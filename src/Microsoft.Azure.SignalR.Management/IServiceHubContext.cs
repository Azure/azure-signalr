// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A context abstraction for a hub.
    /// </summary>
    public interface IServiceHubContext : IHubContext<Hub>
    {
        /// <summary>
        /// Performs a negotiation operation asynchronously that routes a client to a Azure SignalR instance.
        /// </summary>
        /// <returns>A negotiation response object that contains an endpoint url and an access token for the client to connect to the Azure SignalR instance. </returns>
        Task<NegotiationResponse> NegotiateAsync(NegotiationOptions negotiationOptions = null);

        /// <summary>
        /// Gets a user group manager instance which implements <see cref="IUserGroupManager"/> that can be used to add and remove users to named groups.
        /// </summary>
        IUserGroupManager UserGroups { get; }

        /// <summary>
        /// Dispose instances of <see cref="IServiceHubContext"/> asynchronously.
        /// </summary>
        /// <returns></returns>
        Task DisposeAsync();
    }
}