using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal interface IMultiEndpointServiceContainerFactory
    {
        void AddMultipleEndpointServiceConnectionContainer(string hub, IMultiEndpointServiceConnectionContainer container);

        bool TryGetMultiEndpointServiceConnection(string hub, out IMultiEndpointServiceConnectionContainer container);

        IEnumerable<string> GetHubs();
    }
}
