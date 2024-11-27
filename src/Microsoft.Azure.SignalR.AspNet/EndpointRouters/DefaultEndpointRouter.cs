// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Owin;

namespace Microsoft.Azure.SignalR.AspNet;

internal class DefaultEndpointRouter : DefaultMessageRouter, IEndpointRouter
{
    /// <summary>
    /// Randomly select from the available endpoints
    /// </summary>
    /// <param name="owinContext">The incoming owin http context</param>
    /// <param name="endpoints"></param>
    /// <returns></returns>
    public ServiceEndpoint GetNegotiateEndpoint(IOwinContext owinContext, IEnumerable<ServiceEndpoint> endpoints)
    {
        // get primary endpoints snapshot
        var availableEndpoints = GetNegotiateEndpoints(endpoints);
        return availableEndpoints[StaticRandom.Next(availableEndpoints.Length)];
    }

    /// <summary>
    /// Only primary endpoints will be returned by client /negotiate
    /// If no primary endpoint is available, promote one secondary endpoint
    /// </summary>
    /// <returns>The availbale endpoints</returns>
    private ServiceEndpoint[] GetNegotiateEndpoints(IEnumerable<ServiceEndpoint> endpoints)
    {
        var primary = endpoints.Where(s => s.Online && s.EndpointType == EndpointType.Primary).ToArray();
        if (primary.Length > 0)
        {
            return primary;
        }

        // All primary endpoints are offline, fallback to the first online secondary endpoint
        var secondary = endpoints.Where(s => s.Online && s.EndpointType == EndpointType.Secondary).ToArray();
        if (secondary.Length == 0)
        {
            throw new AzureSignalRNotConnectedException();
        }

        return secondary;
    }
}
