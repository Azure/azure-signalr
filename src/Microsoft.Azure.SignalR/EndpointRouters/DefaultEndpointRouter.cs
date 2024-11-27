// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR;

internal class DefaultEndpointRouter : DefaultMessageRouter, IEndpointRouter
{
    /// <summary>
    /// Select an endpoint for negotiate request
    /// </summary>
    /// <param name="context">The http context of the incoming request</param>
    /// <param name="endpoints">All the available endpoints</param>
    public ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> endpoints)
    {
        // get primary endpoints snapshot
        var availableEndpoints = GetNegotiateEndpoints(endpoints);
        return GetEndpointAccordingToWeight(availableEndpoints);
    }

    /// <summary>
    /// Only primary endpoints will be returned by client /negotiate
    /// If no primary endpoint is available, promote one secondary endpoint
    /// </summary>
    /// <returns>The available endpoints</returns>
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

    /// <summary>
    ///  Choose endpoint randomly by weight. 
    ///  The weight is defined as (the remaining connection quota / the connection capacity).
    /// </summary>
    private ServiceEndpoint GetEndpointAccordingToWeight(ServiceEndpoint[] availableEndpoints)
    {
        //first check if weight is available or necessary
        if (availableEndpoints.Any(endpoint => endpoint.EndpointMetrics.ConnectionCapacity == 0) ||
            availableEndpoints.Length == 1)
        {
            return availableEndpoints[StaticRandom.Next(availableEndpoints.Length)];
        }

        var we = new int[availableEndpoints.Length];
        var totalCapacity = 0;
        for (var i = 0; i < availableEndpoints.Length; i++)
        {
            var endpointMetrics = availableEndpoints[i].EndpointMetrics;
            var remain = endpointMetrics.ConnectionCapacity -
                         (endpointMetrics.ClientConnectionCount +
                          endpointMetrics.ServerConnectionCount);
            var weight = Math.Max((int)((double)remain / endpointMetrics.ConnectionCapacity * 1000), 1);
            totalCapacity += weight;
            we[i] = totalCapacity;
        }

        var index = StaticRandom.Next(totalCapacity);
        
        return availableEndpoints[Array.FindLastIndex(we, x => x <= index) + 1];
    }
}