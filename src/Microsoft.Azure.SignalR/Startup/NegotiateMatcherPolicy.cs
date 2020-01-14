// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETSTANDARD2_0
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class NegotiateMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
    {
        // This caches the replacement endpoints for negotiate so they are not recomputed on every request
        private readonly ConcurrentDictionary<Type, Endpoint> _negotiateEndpointCache = new ConcurrentDictionary<Type, Endpoint>();
        
        public override int Order => 1;

        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        {
            foreach (var endpoint in endpoints)
            {
                var hubMetadata = endpoint.Metadata.GetMetadata<HubMetadata>();
                var negotiateMetadata = endpoint.Metadata.GetMetadata<NegotiateMetadata>();

                if (hubMetadata != null && negotiateMetadata != null)
                {
                    return true;
                }
            }

            return false;
        }

        public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                ref var candidate = ref candidates[i];
                // Only apply to RouteEndpoint
                if (candidate.Endpoint is RouteEndpoint routeEndpoint)
                {
                    var hubType = routeEndpoint.Metadata.GetMetadata<HubMetadata>().HubType;
                    var newEndpoint = _negotiateEndpointCache.GetOrAdd(hubType, e => CreateNegotiateEndpoint(routeEndpoint));

                    candidates.ReplaceEndpoint(i, newEndpoint, candidate.Values);
                }
            }

            return Task.CompletedTask;
        }

        private Endpoint CreateNegotiateEndpoint(RouteEndpoint routeEndpoint)
        {
            var hubMetadata = routeEndpoint.Metadata.GetMetadata<HubMetadata>();

            // Replaces the negotiate endpoint with one that does the service redirect
            var routeEndpointBuilder = new RouteEndpointBuilder(async context =>
            {
                await ServiceRouteHelper.RedirectToService(context, hubMetadata.HubType.Name, null);
            },
            routeEndpoint.RoutePattern,
            routeEndpoint.Order);

            // Set DisplayName
            routeEndpointBuilder.DisplayName = routeEndpoint.DisplayName;

            // Preserve the metadata
            foreach (var metadata in routeEndpoint.Metadata)
            {
                routeEndpointBuilder.Metadata.Add(metadata);
            }

            return routeEndpointBuilder.Build();
        }
    }
}
#endif