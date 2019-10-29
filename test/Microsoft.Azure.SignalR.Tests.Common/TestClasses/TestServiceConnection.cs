// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    internal sealed class TestServiceConnection : ServiceConnectionBase
    {
        private readonly bool _throws;
        private ConnectionContext _connection;

        public TestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Connected, bool throws = false) : base(null, null, null, null, ServerConnectionType.Default, NullLogger.Instance)
        {
            Status = status;
            _throws = throws;
        }

        public void SetStatus(ServiceConnectionStatus status)
        {
            Status = status;
        }

        protected override Task CleanupConnections(string instanceId = null)
        {
            return Task.CompletedTask;
        }

        protected override Task<ConnectionContext> CreateConnection(string target = null)
        {
            var pipeOptions = new PipeOptions();
            var duplex = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);
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
            return Status == ServiceConnectionStatus.Connected;
        }

        protected override Task DisposeConnection(ConnectionContext connection)
        {
            return Task.CompletedTask;
        }

        protected override Task OnConnectedAsync(OpenConnectionMessage openConnectionMessage)
        {
            return Task.CompletedTask;
        }

        protected override Task OnDisconnectedAsync(CloseConnectionMessage closeConnectionMessage)
        {
            return Task.CompletedTask;
        }

        protected override Task OnMessageAsync(ConnectionDataMessage connectionDataMessage)
        {
            return Task.CompletedTask;
        }

        public override Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (_throws)
            {
                throw new ServiceConnectionNotActiveException();
            }

            return Task.CompletedTask;
        }

        public void Stop()
        {
            _connection?.Transport.Input.CancelPendingRead();
        }
    }
}
