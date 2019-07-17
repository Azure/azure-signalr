// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal sealed class TestServiceConnection : ServiceConnectionBase
    {
        private readonly ServiceConnectionStatus _status;
        private readonly bool _throws;

        public TestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Connected, bool throws = false) : base(null, null, null, null, ServerConnectionType.Default, null)
        {
            _status = status;
            _throws = throws;
        }

        protected override Task CleanupConnections()
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

        protected override Task<bool> HandshakeAsync()
        {
            return Task.FromResult(_status == ServiceConnectionStatus.Connected);
        }

        protected override Task DisposeConnection()
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
    }
}
