using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Common.Interfaces
{
    interface IServiceConnectionFactory
    {
        Task CreateAsync(ServerConnectionType type);

        Task DisposeAsync();
    }
}
