// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Common.ServiceConnections
{
    internal class WeakServiceConnectionContainer : ServiceConnectionContainerBase
    {
        public WeakServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory, int fixedConnectionCount, ServiceEndpoint endpoint)
            : base(serviceConnectionFactory, connectionFactory, fixedConnectionCount, endpoint)
        {
        }

        // For test purpose only
        internal WeakServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory, List<IServiceConnection> initialConnections, ServiceEndpoint endpoint)
            : base(serviceConnectionFactory, connectionFactory, initialConnections, endpoint)
        {
        }

        protected override IServiceConnection CreateServiceConnectionCore()
        {
            return CreateServiceConnectionCore(ServerConnectionType.Weak);
        }

        public override Task HandlePingAsync(PingMessage pingMessage)
        {
            return Task.CompletedTask;
        }
    }
}
