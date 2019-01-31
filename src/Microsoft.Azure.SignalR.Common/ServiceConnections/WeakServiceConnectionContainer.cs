using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
            IConnectionFactory connectionFactory, ConcurrentDictionary<int?, IServiceConnection> initialConnections, ServiceEndpoint endpoint)
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

            var result = FixedServiceConnections.FirstOrDefault(x => x.Value == serviceConnection);
            if (result.Key.HasValue)
            {
                await RestartServiceConnectionCoreAsync(result.Key.Value);
            }
        }
    }
}
