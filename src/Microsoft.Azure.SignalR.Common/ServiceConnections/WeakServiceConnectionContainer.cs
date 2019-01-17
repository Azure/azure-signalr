using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Common.ServiceConnections
{
    class WeakServiceConnectionContainer : ServiceConnectionContainerBase
    {
        public WeakServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory, 
            IConnectionFactory connectionFactory, 
            int fixedConnectionCount) : base(serviceConnectionFactory, connectionFactory, fixedConnectionCount)
        {
        }

        // For test purpose only
        internal WeakServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory, List<IServiceConnection> initialConnections) : base(
            serviceConnectionFactory, connectionFactory, initialConnections)
        {
        }

        protected override IServiceConnection GetSingleServiceConnection()
        {
            return GetSingleServiceConnection(ServerConnectionType.Weak);
        }

        public override IServiceConnection CreateServiceConnection()
        {
            throw new NotSupportedException();
        }

        public override void DisposeServiceConnection(IServiceConnection connection)
        {
            throw new NotImplementedException();
        }
    }
}
