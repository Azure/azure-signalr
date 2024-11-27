// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Owin;

namespace Microsoft.Azure.SignalR.AspNet;

public interface IEndpointRouter: IMessageRouter
{
    /// <summary>
    /// Get the service endpoint for the client to connect to
    /// </summary>
    /// <param name="owinContext">The incoming owin http context</param>
    /// <param name="endpoints">All the available endpoints</param>
    /// <returns></returns>
    ServiceEndpoint GetNegotiateEndpoint(IOwinContext owinContext, IEnumerable<ServiceEndpoint> endpoints);
}
