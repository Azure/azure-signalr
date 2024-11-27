// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.SignalR.Management;

/// <summary>
/// An endpoint router that only checks health when there are multiple endpoints.
/// </summary>
internal class AutoHealthCheckRouter : EndpointRouterDecorator
{
    public override ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> endpoints)
    {
        return endpoints.Count() == 1 ? endpoints.Single() : base.GetNegotiateEndpoint(context, endpoints);
    }
}