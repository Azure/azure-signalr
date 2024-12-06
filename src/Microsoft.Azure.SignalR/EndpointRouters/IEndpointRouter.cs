// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.SignalR;

public interface IEndpointRouter : IMessageRouter
{
    /// <summary>
    /// Get the service endpoint for the client to connect to
    /// </summary>
    /// <param name="context">The http context of the incoming request</param>
    /// <param name="endpoints">All the available endpoints</param>
    /// <returns></returns>
    ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> endpoints);
}
