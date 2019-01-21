using System;
using System.Collections.Generic;

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

        public override IServiceConnection CreateServiceConnection()
        {
            throw new NotSupportedException();
        }

        public override void DisposeServiceConnection(IServiceConnection connection)
        {
            _ = RestartServiceConnectionAsync(connection);
        }
    }
}
