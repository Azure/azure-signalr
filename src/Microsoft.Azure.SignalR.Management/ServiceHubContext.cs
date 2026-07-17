// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Claims;
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

        /// <summary>
        /// Gets a streaming manager instance which implements <see cref="IStreamingManager"/> that can be used to send streaming messages to clients.
        /// </summary>
        public virtual StreamingManager Streaming => null;

        public virtual IHubClients Clients => null;

        public virtual GroupManager Groups => null;
        IGroupManager IHubContext<Hub>.Groups => Groups;

        public virtual ClientManager ClientManager => null;

        /// <summary>
        /// Performs a negotiation operation asynchronously that routes a client to a Azure SignalR instance.
        /// </summary>
        /// <returns>A negotiation response object that contains an endpoint url and an access token for the client to connect to the Azure SignalR instance. </returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual ValueTask<NegotiationResponse> NegotiateAsync(NegotiationOptions negotiationOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        /// <summary>
        /// Performs a negotiation that also returns the client access token's lifetime seconds.
        /// </summary>
        public virtual Task<NegotiationResult> NegotiateWithTokenLifetimeAsync(NegotiationOptions negotiationOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        /// <summary>
        /// Refreshes the authentication expiration and the application claims of a live client connection without reconnecting,
        /// then returns a refreshed service access token for the client.
        /// </summary>
        /// <param name="connectionToken">The connection token (the <c>id</c> from the client-facing refresh endpoint).</param>
        /// <param name="expireTime">The new authentication expiration deadline (UTC).</param>
        /// <param name="claims">The projected application claim set; applied after the same-user check passes. When null, the refresh is expiration-only.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public virtual Task<RefreshConnectionAuthenticationResult> RefreshConnectionAuthenticationAsync(string connectionToken, DateTimeOffset expireTime, IEnumerable<Claim> claims = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        /// <summary>
        /// Reads the current claim set of a live client connection.
        /// </summary>
        /// <param name="connectionToken">The connection token (the <c>id</c> from the client-facing refresh endpoint).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public virtual Task<ConnectionClaims> GetConnectionClaimsAsync(string connectionToken, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public virtual Task DisposeAsync() => Task.CompletedTask;

        public virtual void Dispose()
        {
            DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get the service endpoints of the hub.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual IEnumerable<ServiceEndpoint> GetServiceEndpoints() => throw new NotImplementedException();

        /// <summary>
        /// Creates and gets a new service hub context instance with specified endpoints. In the new service hub context instance, the router is disabled and all the traffic is sent to the specified endpoints.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual ServiceHubContext WithEndpoints(IEnumerable<ServiceEndpoint> endpoints) => throw new NotImplementedException();
    }
}
