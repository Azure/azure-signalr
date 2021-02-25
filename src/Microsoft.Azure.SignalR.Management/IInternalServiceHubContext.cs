// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;

namespace Microsoft.Azure.SignalR.Management
{
    internal interface IInternalServiceHubContext : IServiceHubContext
    {
        /// <summary>
        /// Gets client endpoint access information object for SignalR hub connections to connect to Azure SignalR Service
        /// </summary>
        /// <returns>Client endpoint and access token to Azure SignalR Service.</returns>
        Task<NegotiationResponse> NegotiateAsync(NegotiationOptions options = null, CancellationToken cancellationToken = default);

        IEnumerable<ServiceEndpoint> GetServiceEndpoints();

        IInternalServiceHubContext WithEndpoints(IEnumerable<ServiceEndpoint> endpoints);
    }
}