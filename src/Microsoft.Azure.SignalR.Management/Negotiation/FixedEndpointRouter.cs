// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.SignalR.Management;

/// <summary>
/// An endpoint router that always return a fixed collection of endpoints.
/// </summary>
internal class FixedEndpointRouter(IEnumerable<ServiceEndpoint> serviceEndpoints) : EndpointRouterDecorator
{
    private readonly IEnumerable<ServiceEndpoint> _serviceEndpoints = serviceEndpoints;

    public override ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> endpoints)
    {
        return _serviceEndpoints.First();
    }
}