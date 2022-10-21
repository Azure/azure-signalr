// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceLifetimeManager<THub> : ServiceLifetimeManagerBase<THub> where THub : Hub
    {
        private const string MarkerNotConfiguredError =
            "'AddAzureSignalR(...)' was called without a matching call to 'IApplicationBuilder.UseAzureSignalR(...)'.";
        private readonly IClientInvocationManager _clientInvocationManager;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly string _callerId;

        public ServiceLifetimeManager(
            IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IHubProtocolResolver protocolResolver,
            ILogger<ServiceLifetimeManager<THub>> logger,
            AzureSignalRMarkerService marker,
            IOptions<HubOptions> globalHubOptions,
            IOptions<HubOptions<THub>> hubOptions,
            IBlazorDetector blazorDetector,
            IServerNameProvider nameProvider,
            IClientInvocationManager clientInvocationManager)
            : base(
                  serviceConnectionManager,
                  protocolResolver,
                  globalHubOptions,
                  hubOptions,
                  logger)
        {
            // after core 3.0 UseAzureSignalR() is not required.
#if NETSTANDARD2_0
            if (!marker.IsConfigured)
            {
                throw new InvalidOperationException(MarkerNotConfiguredError);
            }
#endif
            if (hubOptions.Value.SupportedProtocols != null && hubOptions.Value.SupportedProtocols.Any(x => x.Equals(Constants.Protocol.BlazorPack, StringComparison.OrdinalIgnoreCase)))
            {
                blazorDetector?.TrySetBlazor(typeof(THub).Name, true);
            }

            if (nameProvider == null)
            {
                throw new ArgumentNullException(nameof(nameProvider));
            }
            _callerId = nameProvider.GetName();

            _clientInvocationManager = clientInvocationManager ?? throw new ArgumentNullException(nameof(clientInvocationManager));
            _clientConnectionManager = clientConnectionManager ?? throw new ArgumentNullException(nameof(clientConnectionManager));
        }

        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            var userIdFeature = connection.Features.Get<ServiceUserIdFeature>();
            if (userIdFeature != null)
            {
                connection.UserIdentifier = userIdFeature.UserId;
                connection.Features.Set<ServiceUserIdFeature>(null);
            }
            return base.OnConnectedAsync(connection);
        }

        public override async Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var serviceConnectionContext))
            {
                var message = CreateMessage(connectionId, methodName, args, serviceConnectionContext);
                var messageWithTracingId = (IMessageWithTracingId)message;
                try
                {
                    // Write directly to this connection
                    await serviceConnectionContext.ServiceConnection.WriteAsync(message);

                    if (messageWithTracingId.TracingId != null)
                    {
                        MessageLog.SucceededToSendMessage(Logger, messageWithTracingId);
                    }
                }
                catch (ServiceConnectionNotActiveException)
                {
                    // Fallback to send message through other server connections
                    // Although in current design the server connection drop leads to routed client connection drops
                    // The message thrown here is misleading to the customer
                    // Also sending the message back provides the support when later we support client connection migration
                    await WriteAsync(message);
                }
            }
            else
            {
                await base.SendConnectionAsync(connectionId, methodName, args, cancellationToken);
            }
        }

#if NET7_0_OR_GREATER
        public override async Task<T> InvokeConnectionAsync<T>(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId))
            {
                throw new ArgumentNullException(nameof(connectionId));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            var invocationId = _clientInvocationManager.Caller.GenerateInvocationId(connectionId);

            // Exception handling follows https://source.dot.net/#Microsoft.AspNetCore.SignalR.Core/DefaultHubLifetimeManager.cs,349
            try
            {
                var message = AppendMessageTracingId(new ClientInvocationMessage(invocationId, connectionId, _callerId, SerializeAllProtocols(methodName, args, invocationId)));
                await WriteAsync(message);

                string instanceId = null;
                if (_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var clientConnectionContext))
                {
                    instanceId = clientConnectionContext.InstanceId;
                }
                var task = _clientInvocationManager.Caller.AddInvocation<T>(connectionId, invocationId, instanceId, cancellationToken);
                return await task;
            }
            catch(Exception e)
            {
                // Use `TryCompleteResult` to remove the failed invocation.
                // If the invocation was not added by caller, it will be ignored.
                // The content of error message is useless and will not be exposed to client.
                _clientInvocationManager.Caller.TryCompleteResult(connectionId, CompletionMessage.WithError(invocationId, ""));
                Log.FailedToInvokeClient(Logger, connectionId, methodName, invocationId, e);
                throw;
            }
        }

        // Only route server will reach here
        public override async Task SetConnectionResultAsync(string connectionId, CompletionMessage result)
        {
            if (IsInvalidArgument(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }
            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var clientConnectionContext))
            {
                // Determine which manager (Caller / Router) the `result` belongs to.
                // `TryCompletionResult` returns false when the corresponding invocation is not existing.
                IClientResultsManager clientResultsManager = null;
                if (_clientInvocationManager.Caller.TryCompleteResult(connectionId, result))
                {
                    clientResultsManager = _clientInvocationManager.Caller;
                }
                if (_clientInvocationManager.Router.TryCompleteResult(connectionId, result))
                {
                    clientResultsManager = _clientInvocationManager.Router;
                }

                // Block unknown `results` which belongs to neither Caller nor Router
                if (clientResultsManager != null)
                {
                    var protocol = clientConnectionContext.Protocol;
                    var payload = clientResultsManager == _clientInvocationManager.Router
                                ? SerializeCompletionMessage(result, protocol)
                                : Array.Empty<byte>();

                    var message = AppendMessageTracingId(new ClientCompletionMessage(result.InvocationId, connectionId, _callerId, protocol, payload));
                    await WriteAsync(message);
                }
            }
        }

        public override bool TryGetReturnType(string invocationId, [NotNullWhen(true)] out Type type)
        {
            if (_clientInvocationManager.Router.ContainsInvocation(invocationId))
            {
                return _clientInvocationManager.Router.TryGetInvocationReturnType(invocationId, out type);
            }
            else
            {
                return _clientInvocationManager.Caller.TryGetInvocationReturnType(invocationId, out type);
            }
        }
#endif

        private MultiConnectionDataMessage CreateMessage(string connectionId, string methodName, object[] args, ClientConnectionContext serviceConnectionContext)
        {
            IDictionary<string, ReadOnlyMemory<byte>> payloads;
            if (serviceConnectionContext.Protocol != null)
            {
                payloads = new Dictionary<string, ReadOnlyMemory<byte>>()
                {
                    { serviceConnectionContext.Protocol, SerializeProtocol(serviceConnectionContext.Protocol, methodName, args) }
                };
            }
            else
            {
                payloads = SerializeAllProtocols(methodName, args);
            }

            // don't use ConnectionDataMessage here, since handshake message is also wrapped into ConnectionDataMessage.
            // otherwise it may cause the handshake failure due to hub invocation message is sent to client before handshake message, when there's high preasure on server.
            // do use ConnectionDataMessage when the message is sent from client.
            var message = new MultiConnectionDataMessage(new[] { connectionId }, payloads).WithTracingId();
            if (message.TracingId != null)
            {
                MessageLog.StartToSendMessageToConnections(Logger, message);
            }
            return message;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, string, string, string, Exception> _failedToInvokeClient =
                LoggerMessage.Define<string, string, string, string>(
                    LogLevel.Warning, 
                    new EventId(0, "FailedToInvokeClient"), 
                    "Failed to invoke client {connectionId} with MethodName {methodName} and InvocationId {invocationId}. Detailed exception message is {exception}.");

            public static void FailedToInvokeClient(ILogger logger, string connectionId, string methodName, string invocationId, Exception e)
            {
                _failedToInvokeClient(logger, connectionId, methodName, invocationId, e.Message, e);
            }
        }
    }
}
