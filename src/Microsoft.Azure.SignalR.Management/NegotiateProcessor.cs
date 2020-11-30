// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;

namespace Microsoft.Azure.SignalR.Management
{
    internal class NegotiateProcessor
    {
        public Task<NegotiationResponse> GetClientEndpointAsync(string hubName, HttpContext httpContext = null, string userId = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}