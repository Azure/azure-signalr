// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_0
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Startup
{
    internal class NegotiateMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
    {
        // This caches the replacement endpoints for negotiate so they are not recomputed on every request
        private readonly ConcurrentDictionary<Endpoint, Endpoint> _negotiateEndpointCache = new ConcurrentDictionary<Endpoint, Endpoint>();

        public override int Order => 1;

        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        {
            for (int i = 0; i < endpoints.Count; i++)
            {
                var endpoint = endpoints[i];
                var hubMetadata = endpoint.Metadata.GetMetadata<HubMetadata>();

                if (hubMetadata != null && endpoint is RouteEndpoint route && route.RoutePattern.RawText.EndsWith(Constants.Path.Negotiate))
                {
                    return true;
                }
            }

            return false;
        }

        public Task ApplyAsync(HttpContext httpContext, EndpointSelectorContext context, CandidateSet candidates)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                ref var candidate = ref candidates[i];
                var endpoint = candidate.Endpoint as RouteEndpoint;

                var newEndpoint = _negotiateEndpointCache.GetOrAdd(candidate.Endpoint, CreateNegotiateEndpoint);

                candidates.ReplaceEndpoint(i, newEndpoint, candidate.Values);
            }

            return Task.CompletedTask;
        }

        private static Endpoint CreateNegotiateEndpoint(Endpoint endpoint)
        {
            var routeEndpoint = (RouteEndpoint)endpoint;
            var hubMetadata = endpoint.Metadata.GetMetadata<HubMetadata>();

            // TODO: better to build authorize policy in hub level instead of per request
            var authorizeData = ServiceRouteHelper.BuildAuthorizePolicy(hubMetadata.HubType);

            // This replaces the negotiate endpoint with one that does the service redirect
            var routeEndpointBuilder = new RouteEndpointBuilder(async context =>
            {
                await ServiceRouteHelper.RedirectToService(context, hubMetadata.HubType.Name, authorizeData);
            },
            routeEndpoint.RoutePattern,
            routeEndpoint.Order);

            // Preserve the metadata
            foreach (var metadata in endpoint.Metadata)
            {
                routeEndpointBuilder.Metadata.Add(metadata);
            }

            return routeEndpointBuilder.Build();
        }
    }
}
#endif