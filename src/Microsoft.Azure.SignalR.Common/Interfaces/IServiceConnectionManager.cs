using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    interface IServiceConnectionManager
    {
        IEnumerable<IServiceConnection> CreateServiceConnection(int count = 1);

        void DisposeServiceConnection(IServiceConnection connection);
    }
}
