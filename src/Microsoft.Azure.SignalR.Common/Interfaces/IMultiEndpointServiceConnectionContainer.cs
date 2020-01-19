// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal interface IMultiEndpointServiceConnectionContainer : IServiceConnectionContainer
    {
        /// <summary>
        /// Create IServiceConnectionContainer for new HubServiceEndpoint and start server connections
        /// </summary>
        /// <param name="hubServiceEndpoint"></param>
        /// <param name="timeout"></param>
        /// <returns>add result</returns>
        Task AddServiceEndpoint(HubServiceEndpoint hubServiceEndpoint, TimeSpan timeout);

        /// <summary>
        /// Remove existing HubServiceEndpoint from MultiEndpointServiceConnectionContainer.
        /// </summary>
        /// <param name="hubServiceEndpoint"></param>
        /// <param name="timeout"></param>
        /// <returns>remove result</returns>
        Task RemoveServiceEndpoint(HubServiceEndpoint hubServiceEndpoint, TimeSpan timeout);
    }
}