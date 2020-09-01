// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Serverless.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    /// <summary>
    /// An modified version of HubConnectionHandler https://github.com/dotnet/aspnetcore/blob/v3.1.7/src/SignalR/server/Core/src/HubConnectionHandler.cs
    /// </summary>
    /// <typeparam name="THub"></typeparam>
    internal class HubProxyHandler<THub> : HubConnectionHandler<THub> where THub: Hub
    {
        internal static TimeSpan DefaultHandshakeTimeout => TimeSpan.FromSeconds(15);
        internal static TimeSpan DefaultKeepAliveInterval => TimeSpan.FromSeconds(15);
        internal static TimeSpan DefaultClientTimeoutInterval => TimeSpan.FromSeconds(30);

        internal const int DefaultMaximumMessageSize = 32 * 1024;
        internal const int DefaultStreamBufferCapacity = 10;
        private readonly IOptionsMonitor<UpstreamOptions> _upstreamSettings;
        private readonly HubLifetimeManager<THub> _lifetimeManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly IHubProtocolResolver _protocolResolver;
        private readonly HubOptions<THub> _hubOptions;
        private readonly HubOptions _globalHubOptions;
        private readonly IUserIdProvider _userIdProvider;
        private readonly bool _enableDetailedErrors;
        private readonly HttpServerlessMessageHandler<THub> _upstream;

        private readonly string _hubName = typeof(THub).Name;

        private readonly List<string> _defaultProtocols = new List<string>();

        public HubProxyHandler(
            IOptionsMonitor<UpstreamOptions> upstreamSettings,
            HubLifetimeManager<THub> lifetimeManager,
            IHubProtocolResolver protocolResolver,
            IOptions<HubOptions> globalHubOptions,
            IOptions<HubOptions<THub>> hubOptions,
            ILoggerFactory loggerFactory,
            IUserIdProvider userIdProvider,
            IServiceScopeFactory serviceScopeFactory,
            HttpServerlessMessageHandler<THub> upstream)
            : base(lifetimeManager, protocolResolver, globalHubOptions, hubOptions, loggerFactory, userIdProvider, serviceScopeFactory)
        {
            _protocolResolver = protocolResolver;
            _upstreamSettings = upstreamSettings;
            _lifetimeManager = lifetimeManager;
            _loggerFactory = loggerFactory;
            _hubOptions = hubOptions.Value;
            _globalHubOptions = globalHubOptions.Value;
            _logger = loggerFactory.CreateLogger<HubConnectionHandler<THub>>();
            _userIdProvider = userIdProvider;
            _upstream = upstream;

            _enableDetailedErrors = true;
        }

        /// <inheritdoc />
        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            // We check to see if HubOptions<THub> are set because those take precedence over global hub options.
            // Then set the keepAlive and handshakeTimeout values to the defaults in HubOptionsSetup when they were explicitly set to null.
            var supportedProtocols = _hubOptions.SupportedProtocols ?? _globalHubOptions.SupportedProtocols;
            if (supportedProtocols == null || supportedProtocols.Count == 0)
            {
                throw new InvalidOperationException("There are no supported protocols");
            }

            var handshakeTimeout = _hubOptions.HandshakeTimeout ?? _globalHubOptions.HandshakeTimeout ?? DefaultHandshakeTimeout;

            var contextOptions = new HubConnectionContextOptions()
            {
                KeepAliveInterval = _hubOptions.KeepAliveInterval ?? _globalHubOptions.KeepAliveInterval ?? DefaultKeepAliveInterval,
                ClientTimeoutInterval = _hubOptions.ClientTimeoutInterval ?? _globalHubOptions.ClientTimeoutInterval ?? DefaultClientTimeoutInterval,
                StreamBufferCapacity = _hubOptions.StreamBufferCapacity ?? _globalHubOptions.StreamBufferCapacity ?? DefaultStreamBufferCapacity,
                MaximumReceiveMessageSize = long.MaxValue,
            };

            Log.ConnectedStarting(_logger);

            var connectionContext = new EmulatorHubConnectionContext(_hubName, connection, contextOptions, _loggerFactory);

            var resolvedSupportedProtocols = (supportedProtocols as IReadOnlyList<string>) ?? supportedProtocols.ToList();
            if (!await InvokeHandshakeAsync(handshakeTimeout, connectionContext, resolvedSupportedProtocols))
            {
                return;
            }

            // -- the connectionContext has been set up --

            try
            {
                await _lifetimeManager.OnConnectedAsync(connectionContext);
                await RunHubAsync(connectionContext);
            }
            finally
            {
                Log.ConnectedEnding(_logger);
                await _lifetimeManager.OnDisconnectedAsync(connectionContext);
            }
        }

        #region Reflection

        private static readonly MethodInfo _hubConnectionContext_HandshakeAsync =
            typeof(HubConnectionContext).GetMethod("HandshakeAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo _base_SendCloseAsync =
            typeof(HubConnectionHandler<THub>).GetMethod("SendCloseAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly PropertyInfo _hubConnectionContext_AllowReconnect =
            typeof(HubConnectionContext).GetProperty("AllowReconnect", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo _hubConnectionContext_AbortAsync =
            typeof(HubConnectionContext).GetMethod("AbortAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly PropertyInfo _hubConnectionContext_Input =
            typeof(HubConnectionContext).GetProperty("Input", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo _hubConnectionContext_ResetClientTimeout =
            typeof(HubConnectionContext).GetMethod("ResetClientTimeout", BindingFlags.Instance | BindingFlags.NonPublic);

        private Task<bool> InvokeHandshakeAsync(TimeSpan handshakeTimeout, HubConnectionContext connectionContext, IReadOnlyList<string> resolvedSupportedProtocols)
        {
            var task = (Task<bool>)_hubConnectionContext_HandshakeAsync.Invoke(connectionContext, new object[] { handshakeTimeout, resolvedSupportedProtocols, _protocolResolver, _userIdProvider, _enableDetailedErrors });
            return task;
        }

        private Task InvokeSendCloseAsync(HubConnectionContext connection, Exception exception, bool allowReconnect)
        {
            var task = (Task)_base_SendCloseAsync.Invoke(this, new object[] { connection, exception, allowReconnect });
            return task;
        }

        private bool GetAllowReconnect(HubConnectionContext connection)
        {
            return (bool)_hubConnectionContext_AllowReconnect.GetValue(connection);
        }

        private Task InvokeAbortAsync(HubConnectionContext connection)
        {
            return (Task)_hubConnectionContext_AbortAsync.Invoke(connection, Array.Empty<object>());
        }

        private PipeReader GetInput(HubConnectionContext connection)
        {
            return (PipeReader)_hubConnectionContext_Input.GetValue(connection);
        }

        private void InvokeResetClientTimeout(HubConnectionContext connection)
        {
            _hubConnectionContext_ResetClientTimeout.Invoke(connection, Array.Empty<object>());
        }

        #endregion

        private async Task RunHubAsync(HubConnectionContext connection)
        {
            try
            {
                await _upstream.AddClientConnectionAsync(connection);
            }
            catch (Exception ex)
            {
                Log.ErrorDispatchingHubEvent(_logger, nameof(_upstream.AddClientConnectionAsync), ex);

                // The client shouldn't try to reconnect given an error in OnConnected.
                await InvokeSendCloseAsync(connection, ex, allowReconnect: false);

                // return instead of throw to let close message send successfully
                return;
            }

            try
            {
                await DispatchMessagesAsync(connection);
            }
            catch (OperationCanceledException)
            {
                // Don't treat OperationCanceledException as an error, it's basically a "control flow"
                // exception to stop things from running
            }
            catch (Exception ex)
            {
                Log.ErrorProcessingRequest(_logger, ex);

                await HubOnDisconnectedAsync(connection, ex);

                // return instead of throw to let close message send successfully
                return;
            }

            await HubOnDisconnectedAsync(connection, null);
        }

        private async Task HubOnDisconnectedAsync(HubConnectionContext connection, Exception exception)
        {
            // send close message before aborting the connection
            await InvokeSendCloseAsync(connection, exception, GetAllowReconnect(connection));

            // We wait on abort to complete, this is so that we can guarantee that all callbacks have fired
            // before OnDisconnectedAsync

            // Ensure the connection is aborted before firing disconnect
            await InvokeAbortAsync(connection);

            try
            {
                await _upstream.RemoveClientConnectionAsync(connection, exception?.Message);
            }
            catch (Exception ex)
            {
                Log.ErrorDispatchingHubEvent(_logger, nameof(_upstream.RemoveClientConnectionAsync), ex);
                throw;
            }
        }

        private async Task DispatchMessagesAsync(HubConnectionContext connection)
        {
            var input = GetInput(connection);
            var protocol = connection.Protocol;

            var binder = InvocationBinder.Instance;

            while (true)
            {
                var result = await input.ReadAsync();
                var buffer = result.Buffer;

                try
                {
                    if (result.IsCanceled)
                    {
                        break;
                    }

                    if (!buffer.IsEmpty)
                    {
                        InvokeResetClientTimeout(connection);

                        // No message limit, just parse and dispatch
                        while (TryParse(protocol, binder, ref buffer, out var message))
                        {
                            if (message is InvocationMessage invocationMessage)
                            {
                                var invocation = new ServerlessProtocol.InvocationMessage(new ReadOnlySequence<byte>(protocol.GetMessageBytes(invocationMessage)), invocationMessage.Target, invocationMessage.InvocationId);
                                var response = await ((IUpstreamMessageHandler)_upstream).WriteMessageAsync(connection, invocation);
                                if (response.Length > 0)
                                {
                                    await ((EmulatorHubConnectionContext)connection).ConnectionContext.Transport.Output.WriteAsync(response.First);
                                }
                            }
                        }
                    }

                    if (result.IsCompleted)
                    {
                        if (!buffer.IsEmpty)
                        {
                            throw new InvalidDataException("Connection terminated while reading a message.");
                        }
                        break;
                    }
                }
                finally
                {
                    // The buffer was sliced up to where it was consumed, so we can just advance to the start.
                    // We mark examined as buffer.End so that if we didn't receive a full frame, we'll wait for more data
                    // before yielding the read again.
                    input.AdvanceTo(buffer.Start, buffer.End);
                }
            }
        }

        private static bool TryParse(IHubProtocol protocol, IInvocationBinder[] binders, ref ReadOnlySequence<byte> buffer, out HubMessage message)
        {
            ReadOnlySequence<byte> seq = buffer;
            message = null;
            foreach (var binder in binders)
            {
                seq = buffer;
                if (protocol.TryParseMessage(ref seq, binder, out message) && !(message is InvocationBindingFailureMessage))
                {
                    buffer = seq;
                    return true;
                }
            }
            buffer = seq;
            return false;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _errorDispatchingHubEvent =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(1, "ErrorDispatchingHubEvent"), "Error when dispatching '{HubMethod}' on hub.");

            private static readonly Action<ILogger, Exception> _errorProcessingRequest =
                LoggerMessage.Define(LogLevel.Debug, new EventId(2, "ErrorProcessingRequest"), "Error when processing requests.");

            private static readonly Action<ILogger, Exception> _abortFailed =
                LoggerMessage.Define(LogLevel.Trace, new EventId(3, "AbortFailed"), "Abort callback failed.");

            private static readonly Action<ILogger, Exception> _errorSendingClose =
                LoggerMessage.Define(LogLevel.Debug, new EventId(4, "ErrorSendingClose"), "Error when sending Close message.");

            private static readonly Action<ILogger, Exception> _connectedStarting =
                LoggerMessage.Define(LogLevel.Debug, new EventId(5, "ConnectedStarting"), "OnConnectedAsync started.");

            private static readonly Action<ILogger, Exception> _connectedEnding =
                LoggerMessage.Define(LogLevel.Debug, new EventId(6, "ConnectedEnding"), "OnConnectedAsync ending.");

            public static void ErrorDispatchingHubEvent(ILogger logger, string hubMethod, Exception exception)
            {
                _errorDispatchingHubEvent(logger, hubMethod, exception);
            }

            public static void ErrorProcessingRequest(ILogger logger, Exception exception)
            {
                _errorProcessingRequest(logger, exception);
            }

            public static void AbortFailed(ILogger logger, Exception exception)
            {
                _abortFailed(logger, exception);
            }

            public static void ErrorSendingClose(ILogger logger, Exception exception)
            {
                _errorSendingClose(logger, exception);
            }

            public static void ConnectedStarting(ILogger logger)
            {
                _connectedStarting(logger, null);
            }

            public static void ConnectedEnding(ILogger logger)
            {
                _connectedEnding(logger, null);
            }
        }

        private sealed class InvocationBinder : IInvocationBinder
        {
            public static readonly IInvocationBinder[] Instance = new[]
            {
                new InvocationBinder(0),
                new InvocationBinder(1),
                new InvocationBinder(2),
                new InvocationBinder(3),
                new InvocationBinder(4),
                new InvocationBinder(5),
                new InvocationBinder(6),
                new InvocationBinder(7),
                new InvocationBinder(8),
                new InvocationBinder(9),
                new InvocationBinder(10),
            };

            private readonly Type[] _args;

            public InvocationBinder(int count)
            {
                _args = new Type[count];
                _args.AsSpan().Fill(typeof(object));
            }

            public IReadOnlyList<Type> GetParameterTypes(string methodName)
            {
                return _args;
            }

            public Type GetReturnType(string invocationId)
            {
                return typeof(object);
            }

            public Type GetStreamItemType(string streamId)
            {
                return typeof(object);
            }
        }
    }
}
