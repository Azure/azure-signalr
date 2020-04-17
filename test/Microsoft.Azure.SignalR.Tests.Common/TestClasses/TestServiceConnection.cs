// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    internal class TestServiceConnection : ServiceConnectionBase
    {
        private readonly bool _throws;
        private readonly ILogger _logger;

        private ServiceConnectionStatus _expectedStatus;

        private ConnectionContext _connection;

        public IDuplexPipe Application { get; private set; }

        private TaskCompletionSource<object> _created = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ConnectionCreated => _created.Task;

        public TestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Connected, bool throws = false,
            ILogger logger = null,
            IServiceMessageHandler serviceMessageHandler = null
            ) : base(
            new ServiceProtocol(),
            "serverId",
            Guid.NewGuid().ToString(),
            new HubServiceEndpoint(),
            serviceMessageHandler,
            ServiceConnectionType.Default,
            ServerConnectionMigrationLevel.Off,
            logger ?? NullLogger.Instance
        )
        {
            _expectedStatus = status;
            _throws = throws;
            _logger = logger ?? NullLogger.Instance;
        }

        public void SetStatus(ServiceConnectionStatus status)
        {
            Status = status;
            _expectedStatus = status;
        }

        protected override Task CleanupClientConnections(string fromInstanceId = null)
        {
            return Task.CompletedTask;
        }

        protected override Task<ConnectionContext> CreateConnection(string target = null)
        {
            var pipeOptions = new PipeOptions();
            var duplex = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);

            Application = duplex.Application;
            _created.SetResult(null);

            return Task.FromResult<ConnectionContext>(new DefaultConnectionContext()
            {
                Application = duplex.Application,
                Transport = duplex.Transport
            });
        }

        protected override async Task<bool> HandshakeAsync(ConnectionContext connection)
        {
            _connection = connection;
            await Task.Yield();
            return _expectedStatus == ServiceConnectionStatus.Connected;
        }

        protected override Task DisposeConnection(ConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        protected override Task OnClientConnectedAsync(OpenConnectionMessage openConnectionMessage)
        {
            return Task.CompletedTask;
        }

        protected override Task OnClientDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            return Task.CompletedTask;
        }

        protected override Task OnClientMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            return Task.CompletedTask;
        }

        protected Task WriteAsyncBase(ServiceMessage serviceMessage)
        {
            return base.WriteAsync(serviceMessage);
        }

        protected override Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
        {
            if (_throws)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public void Stop()
        {
            _connection?.Transport.Input.CancelPendingRead();
        }
    }
}
