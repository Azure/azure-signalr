// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A context abstraction for a hub.
    /// </summary>
    public interface IServiceHubContext : IHubContext<Hub>
    {
        /// <summary>
        /// Gets a user group manager instance which implements <see cref="IUserGroupManager"/> that can be used to add and remove users to named groups.
        /// </summary>
        IUserGroupManager UserGroups { get; }

        /// <summary>
        /// Dispose instances of <see cref="IServiceHubContext"/> asynchronously. 
        /// </summary>
        /// <returns></returns>
        Task DisposeAsync();

        internal IEnumerable<ServiceEndpoint> Endpoints { get; }

        /// <summary>
        /// Creates an instance of <see cref="IServiceHubContext"/> which skips internal router and uses specified service endpoints asynchronously.
        /// </summary>
        internal IServiceHubContext WithEndpoints(IEnumerable<ServiceEndpoint> endpoints);
    }
}