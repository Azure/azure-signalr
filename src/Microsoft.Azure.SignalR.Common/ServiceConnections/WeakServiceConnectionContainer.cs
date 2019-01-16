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
            int count) : base(serviceConnectionFactory, connectionFactory, count)
        {
        }

        // For test purpose only
        public WeakServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            IConnectionFactory connectionFactory, List<IServiceConnection> initialConnections) : base(
            serviceConnectionFactory, connectionFactory, initialConnections)
        {
        }

        protected override IServiceConnection GetSingleServiceConnection()
        {
            return GetSingleServiceConnection(ServerConnectionType.Weak);
        }

        protected override IServiceConnection CreateServiceConnectionCore()
        {
            throw new NotSupportedException();
        }

        public override void DisposeServiceConnection(IServiceConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            int index = ServiceConnections.IndexOf(connection);
            if (index == -1)
            {
                return;
            }

            _ = ReconnectWithDelayAsync(index);
        }
    }
}
