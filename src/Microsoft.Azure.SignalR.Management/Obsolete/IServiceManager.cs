// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management
{
    // TODO mark as obsolete when substitute is ready
    /// <summary>
    /// A manager abstraction for managing Azure SignalR Service.
    /// </summary>
    public interface IServiceManager : IDisposable
    {
        /// <summary>
        /// Creates an instance of <see cref="IServiceHubContext"/> asynchronously.
        /// </summary>
        /// <param name="hubName">The hub name.</param>
        /// <param name="loggerFactory">The logger factory.</param>
        /// <param name="cancellationToken">Cancellation token for creating service hub context.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<IServiceHubContext> CreateHubContextAsync(string hubName, ILoggerFactory loggerFactory = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a client access token for SignalR hub connections to connect to Azure SignalR Service.
        /// </summary>
        /// <param name="hubName">The hub name.</param>
        /// <param name="userId">The user ID.</param>
        /// <param name="claims">The claim list to be put into access token.</param>
        /// <param name="lifeTime">The lifetime of the token. The default value is one hour.</param>
        /// <returns>Client access token to Azure SignalR Service.</returns>
        string GenerateClientAccessToken(string hubName, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null);

        /// <summary>
        /// Creates an client endpoint for SignalR hub connections to connect to Azure SignalR Service
        /// </summary>
        /// <param name="hubName">The hub name.</param>
        /// <returns>Client endpoint to Azure SignalR Service.</returns>
        string GetClientEndpoint(string hubName);

        /// <summary>
        /// Checks the health status of the Azure SignalR Service.
        /// </summary>
        /// <param name="cancellationToken"> The cancellation token.</param>
        /// <returns>The boolean indicates the health of the service</returns>
        Task<bool> IsServiceHealthy(CancellationToken cancellationToken);
    }
}