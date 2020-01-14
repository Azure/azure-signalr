using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal interface IMultiEndpointServiceConnectionContainer : IServiceConnectionContainer
    {
        /// <summary>
        /// Create IServiceConnectionContainer for new ServiceEndpoint and start server connections
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns>add result</returns>
        Task<bool> TryAddServiceEndpoint(HubServiceEndpoint endpoint);

        /// <summary>
        /// Remove existing ServiceEndpoint
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns>remove result</returns>
        Task<bool> TryRemoveServiceEndpoint(HubServiceEndpoint endpoint);
    }
}