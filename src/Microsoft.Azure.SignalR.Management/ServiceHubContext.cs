﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal abstract class ServiceHubContext : IServiceHubContext
    {
        /// <summary>
        /// Gets a user group manager instance which implements <see cref="IUserGroupManager"/> that can be used to add and remove users to named groups.
        /// </summary>
        public virtual IUserGroupManager UserGroups => null;

        public virtual IHubClients Clients => null;

        public virtual IGroupManager Groups => null;

        /// <summary>
        /// Performs a negotiation operation asynchronously that routes a client to a Azure SignalR instance.
        /// </summary>
        /// <returns>A negotiation response object that contains an endpoint url and an access token for the client to connect to the Azure SignalR instance. </returns>
        public virtual Task<NegotiationResponse> NegotiateAsync(NegotiationOptions negotiationOptions = null, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        /// <summary>
        /// Close a connection asynchronously.
        /// </summary>
        /// <param name="connectionId">The connection to close</param>
        /// <param name="reason">The reason to close the connection. </param>
        /// <param name="cancellationToken"></param>
        /// <returns>The created <see cref="System.Threading.Tasks.Task{TResult}">Task</see> that represents the asynchronous operation.</returns>
        /// <remarks>To get the <paramref name="reason"/> from connection closed event, client should set <see cref="NegotiationOptions.EnableDetailedErrors"/> during negotiation.</remarks>
        public virtual Task CloseConnectionAsync(string connectionId, string reason = null, CancellationToken cancellationToken = default) => null;

        public virtual Task DisposeAsync() => Task.CompletedTask;
    }
}