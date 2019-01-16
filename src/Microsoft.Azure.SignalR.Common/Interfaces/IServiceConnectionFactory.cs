using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    interface IServiceConnectionFactory
    {
        IServiceConnection Create(IConnectionFactory connectionFactory, IServiceConnectionManager manager, ServerConnectionType type);
    }
}
