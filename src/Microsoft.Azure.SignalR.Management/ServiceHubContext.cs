// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    public abstract class ServiceHubContext : IServiceHubContext, IDisposable
    {
        /// <summary>
        /// Gets a user group manager instance which implements <see cref="IUserGroupManager"/> that can be used to add and remove users to named groups.
        /// </summary>
        public virtual UserGroupManager UserGroups => null;
        IUserGroupManager IServiceHubContext.UserGroups => UserGroups;

        public virtual IHubClients Clients => null;

        public virtual GroupManager Groups => null;
        IGroupManager IHubContext<Hub>.Groups => Groups;

        public virtual ClientManager ClientManager => null;

        /// <summary>
        /// Performs a negotiation operation asynchronously that routes a client to a Azure SignalR instance.
        /// </summary>
        /// <returns>A negotiation response object that contains an endpoint url and an access token for the client to connect to the Azure SignalR instance. </returns>
        public virtual ValueTask<NegotiationResponse> NegotiateAsync(NegotiationOptions negotiationOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public virtual Task DisposeAsync() => Task.CompletedTask;

        public virtual void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }
}