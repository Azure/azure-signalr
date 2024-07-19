// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Owin;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

internal class TestEndpointRouter : EndpointRouterDecorator
{
    public TestEndpointRouter() : base()
    {
    }

    public override ServiceEndpoint GetNegotiateEndpoint(IOwinContext context, IEnumerable<ServiceEndpoint> endpoints)
    {
        var endpointName = context.Request.Query["endpoint"];
        if (string.IsNullOrEmpty(endpointName))
        {
            context.Response.StatusCode = 400;
            context.Response.Write("Invalid request.");
            return null;
        }

        return endpoints.First(s => s.Name == endpointName && s.Online);
    }
}