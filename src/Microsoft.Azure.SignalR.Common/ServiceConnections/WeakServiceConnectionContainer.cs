using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        public override Task HandlePingAsync(string target)
        {
            throw new NotSupportedException();
        }

        protected override async Task DisposeOrRestartServiceConnectionAsync(IServiceConnection serviceConnection)
        {
            if (serviceConnection == null)
            {
                throw new ArgumentNullException(nameof(serviceConnection));
            }

            int index = FixedServiceConnections.IndexOf(serviceConnection);
            if (index == -1)
            {
                return;
            }

            await RestartServiceConnectionCoreAsync(index);
        }
    }
}
