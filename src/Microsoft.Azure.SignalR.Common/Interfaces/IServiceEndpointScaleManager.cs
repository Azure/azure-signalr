using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Common.Interfaces
{
    internal interface IServiceEndpointScaleManager
    {
        Task StartAddServiceEndpointAsync();

        Task StopAddServiceEndpointAsync();

        Task StartRemoveServiceEndpointAsync();

        Task StopRemoveServiceEndpointAsync();
    }
}
