using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    interface IServiceConnectionManager
    {
        IServiceConnection CreateServiceConnection();

        void HandleConnectionAck(string ackId);

        void DisposeServiceConnection(IServiceConnection connection);
    }
}
