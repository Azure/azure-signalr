// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal class ServiceConnectionProxy : IDisposable
    {
        private static readonly IServiceProtocol ServiceProtocol = new ServiceProtocol();

        private readonly LogWaiterProvider _logWaiterProvider;
        private readonly TestServiceConnection _serviceConnection;

        public ServiceConnectionProxy(IClientConnectionManager clientConnectionManager, ILoggerFactory loggerFactory, ConnectionDelegate callback = null, PipeOptions clientPipeOptions = null)
        {
            _logWaiterProvider = new LogWaiterProvider();
            loggerFactory.AddProvider(_logWaiterProvider);
            var logger = loggerFactory.CreateLogger<ServiceConnectionProxy>();
            var connectionFactory = new TestConnectionFactory();
            _serviceConnection = new TestServiceConnection(connectionFactory, clientConnectionManager, logger, callback, clientPipeOptions);
        }

        public Task StartServiceAsync()
        {
            _ = _serviceConnection.StartAsync();

            return _logWaiterProvider.WaitFor(new EventId(20, "ServiceConnectionConnected"));
        }

        public Task WaitForClientConnectAsync(string connectionId)
        {
            return _serviceConnection.WaitForClientConnectAsync(connectionId);
        }

        public Task WaitForApplicationMessageAsync(string connectionId)
        {
            return _serviceConnection.WaitForApplicationMessageAsync(connectionId);
        }

        public Task WaitForClientDisconnectAsync(string connectionId)
        {
            return _serviceConnection.WaitForClientDisconnectAsync(connectionId);
        }

        public Task WriteMessageAsync(ServiceMessage message)
        {
            return _serviceConnection.WriteMessageAsync(message);
        }

        public void Dispose()
        {
            _serviceConnection.Dispose();
        }

        private sealed class TestConnectionFactory : IConnectionFactory
        {
            private readonly ConnectionDelegate _connectCallback;
            private TaskCompletionSource<TestConnectionContext> _waitForServerConnection = new TaskCompletionSource<TestConnectionContext>();

            public TestConnectionFactory(ConnectionDelegate connectCallback = null)
            {
                _connectCallback = connectCallback ?? OnConnectionAsync;
            }

            public Task<TestConnectionContext> GetConnectedServerAsync()
            {
                return _waitForServerConnection.Task;
            }

            public Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, string hubName, CancellationToken cancellationToken = default)
            {
                var connection = new TestConnectionContext();
                _connectCallback?.Invoke(connection);

                _waitForServerConnection.TrySetResult(connection);
                return Task.FromResult<ConnectionContext>(connection);
            }

            public Task DisposeAsync(ConnectionContext connection)
            {
                return Task.CompletedTask;
            }

            private Task OnConnectionAsync(ConnectionContext connection)
            {
                var tcs = new TaskCompletionSource<object>();

                // Wait for the connection to close
                connection.Transport.Input.OnWriterCompleted((ex, state) =>
                {
                    tcs.TrySetResult(null);
                },
                null);

                return tcs.Task;
            }
        }

        private sealed class TestServiceConnection : ServiceConnection, IDisposable
        {
            private readonly ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>> _waitForConnectionOpen = new ConcurrentDictionary<string, TaskCompletionSource<ConnectionContext>>();
            private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _waitForConnectionClose = new ConcurrentDictionary<string, TaskCompletionSource<object>>();
            private readonly ConcurrentDictionary<string, TaskCompletionSource<ServiceMessage>> _waitForApplicationMessage = new ConcurrentDictionary<string, TaskCompletionSource<ServiceMessage>>();

            public TestConnectionContext ConnectionContext { get; private set; }

            private readonly TaskCompletionSource<ConnectionContext> _waitForServiceDisconnected = new TaskCompletionSource<ConnectionContext>();

            public TestServiceConnection(TestConnectionFactory connectionFactory, IClientConnectionManager clientConnectionManager, ILogger logger, ConnectionDelegate callback = null, PipeOptions clientPipeOptions = null) :
                base(
                    Guid.NewGuid().ToString("N"),
                    Guid.NewGuid().ToString("N"),
                    ServiceProtocol,
                    connectionFactory,
                    clientConnectionManager,
                    logger)
            {
            }

            public Task WaitForServiceDisconnectAsync()
            {
                return _waitForServiceDisconnected.Task;
            }

            protected override async Task<ConnectionContext> CreateConnection()
            {
                ConnectionContext = (TestConnectionContext)await base.CreateConnection();
                await WriteMessageAsync(new HandshakeResponseMessage());
                return ConnectionContext;
            }

            protected override async Task DisposeConnection()
            {
                await base.DisposeConnection();
                _waitForServiceDisconnected.TrySetResult(_connection);
            }

            protected override async Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage)
            {
                await base.OnConnectedAsync(openConnectionMessage);

                var tcs = _waitForConnectionOpen.GetOrAdd(openConnectionMessage.ConnectionId, i => new TaskCompletionSource<ConnectionContext>());

                tcs.TrySetResult(null);
            }

            protected override async Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
            {
                await base.OnDisconnectedAsync(closeConnectionMessage);
                var tcs = _waitForConnectionClose.GetOrAdd(closeConnectionMessage.ConnectionId, i => new TaskCompletionSource<object>());

                tcs.TrySetResult(null);
            }

            protected override async Task OnMessageAsync(ConnectionDataMessage connectionDataMessage)
            {
                await base.OnMessageAsync(connectionDataMessage);

                var tcs = _waitForApplicationMessage.GetOrAdd(connectionDataMessage.ConnectionId, i => new TaskCompletionSource<ServiceMessage>());

                tcs.TrySetResult(connectionDataMessage);
            }

            public Task WaitForClientConnectAsync(string connectionId)
            {
                var tcs = _waitForConnectionOpen.GetOrAdd(connectionId, i => new TaskCompletionSource<ConnectionContext>());

                return tcs.Task;
            }

            public Task WaitForApplicationMessageAsync(string connectionId)
            {
                var tcs = _waitForApplicationMessage.GetOrAdd(connectionId, i => new TaskCompletionSource<ServiceMessage>());

                return tcs.Task;
            }

            public Task WaitForClientDisconnectAsync(string connectionId)
            {
                var tcs = _waitForConnectionClose.GetOrAdd(connectionId, i => new TaskCompletionSource<object>());

                return tcs.Task;
            }

            public async Task WriteMessageAsync(ServiceMessage message)
            {
                if (ConnectionContext == null)
                {
                    throw new InvalidOperationException("Server connection is not yet established.");
                }

                ServiceProtocol.WriteMessage(message, ConnectionContext.Application.Output);
                await ConnectionContext.Application.Output.FlushAsync();
            }

            public void Dispose()
            {
                _ = StopAsync();
            }
        }

        private sealed class TestConnectionContext : ConnectionContext
        {
            public TestConnectionContext()
            {
                Features = new FeatureCollection();
                Items = new ConcurrentDictionary<object, object>();

                var pipeOptions = new PipeOptions();
                var pair = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);
                var proxyToApplication = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);

                Transport = pair.Transport;
                Application = pair.Application;
            }

            public override string ConnectionId { get; set; }

            public override IFeatureCollection Features { get; }

            public override IDictionary<object, object> Items { get; set; }

            public override IDuplexPipe Transport { get; set; }

            public IDuplexPipe Application { get; set; }
        }
    }
}
