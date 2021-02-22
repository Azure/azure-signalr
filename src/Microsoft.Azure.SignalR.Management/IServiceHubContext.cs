// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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
        /// Gets client endpoint access information object for SignalR hub connections to connect to Azure SignalR Service
        /// </summary>
        /// <param name="httpContext">The HTTP context which might provide information for routing and generating access token.</param>
        /// <param name="userId">The user ID. If null, the identity name in <see cref="HttpContext.User" /> of <paramref name="httpContext"/> will be used.</param>
        /// <param name="claims">The claim list to be put into access token. If null, the claims in <see cref="HttpContext.User"/> of <paramref name="httpContext"/> will be used.</param>
        /// <param name="lifetime">The lifetime of the token. The default value is one hour.</param>
        /// <param name="isDiagnosticClient">The flag whether the client to be connected is a diagnostic client.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation. If null, the <see cref="HttpContext.RequestAborted"/> of <paramref name="httpContext"/> will be used. </param>
        /// <returns>Client endpoint and access token to Azure SignalR Service.</returns>
        Task<NegotiationResponse> NegotiateAsync(HttpContext httpContext = null, string userId = null, IList<Claim> claims = null, TimeSpan? lifetime = null, bool isDiagnosticClient = false, CancellationToken cancellationToken = default);

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