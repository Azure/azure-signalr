// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;

namespace Microsoft.Azure.SignalR.Management
{
    public interface IServiceContext : IDisposable
    {
        /// <summary>
        /// Creates an instance of <see cref="IServiceHubContext"/> asynchronously.
        /// </summary>
        /// <param name="hubName">The hub name.</param>
        /// <param name="cancellationToken">Cancellation token for creating service hub context.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<IServiceHubContext> CreateHubContextAsync(string hubName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets client endpoint access information object for SignalR hub connections to connect to Azure SignalR Service
        /// </summary>
        /// <param name="hubName">The hub name.</param>
        /// <param name="httpContext">The HTTP context which might provide information for routing and generating access token.</param>
        /// <param name="userId">The user ID. If null, the identity name in <see cref="HttpContext.User" /> of <paramref name="httpContext"/> will be used.</param>
        /// <param name="claims">The claim list to be put into access token. If null, the claims in <see cref="HttpContext.User"/> of <paramref name="httpContext"/> will be used.</param>
        /// <param name="lifetime">The lifetime of the token. The default value is one hour.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation. If null, the <see cref="HttpContext.RequestAborted"/> of <paramref name="httpContext"/> will be used. </param>
        /// <returns>Client endpoint and access token to Azure SignalR Service.</returns>
        Task<NegotiationResponse> GetClientEndpointAsync(string hubName, HttpContext httpContext = null, string userId = null, IList<Claim> claims = null, TimeSpan? lifetime = null, CancellationToken cancellationToken = default);

        internal IEnumerable<ServiceEndpoint> GetServiceEndpoints(string hubName);
    }
}