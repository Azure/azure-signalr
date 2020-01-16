// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        /// Stop server connection and remove existing ServiceEndpoint from MultiEndpointServiceConnectionContainer.
        /// Better to call OfflineAync() before this to safely clean service side router and drop clients
        /// </summary>
        /// <param name="endpoint"></param>
        /// <returns>remove result</returns>
        Task<bool> TryRemoveServiceEndpoint(HubServiceEndpoint endpoint);

        /// <summary>
        /// Flag of globally stable by comparing inner multiple endpoints connected serverIds with strong consistent
        /// </summary>
        bool IsStable { get; }

        /// <summary>
        /// Get result whether target ServiceEndpoint has active clients
        /// </summary>
        /// <param name="serviceEndpoint"></param>
        /// <returns></returns>
        bool IsEndpointActive(ServiceEndpoint serviceEndpoint);
    }
}