﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_0
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
        private readonly ConcurrentDictionary<string, List<IAuthorizeData>> _hubAuthorizePolicy = new ConcurrentDictionary<string, List<IAuthorizeData>>();
        
        public override int Order => 1;

        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        {
            foreach(var endpoint in endpoints)
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

        public Task ApplyAsync(HttpContext httpContext, EndpointSelectorContext context, CandidateSet candidates)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                ref var candidate = ref candidates[i];
                var newEndpoint = _negotiateEndpointCache.GetOrAdd(candidate.Endpoint, CreateNegotiateEndpoint);

                candidates.ReplaceEndpoint(i, newEndpoint, candidate.Values);
            }

            return Task.CompletedTask;
        }

        private Endpoint CreateNegotiateEndpoint(Endpoint endpoint)
        {
            var routeEndpoint = (RouteEndpoint)endpoint;
            var hubMetadata = endpoint.Metadata.GetMetadata<HubMetadata>();

            // Cache hub authorized policy
            if (!_hubAuthorizePolicy.ContainsKey(hubMetadata.HubType.Name))
            {
                _hubAuthorizePolicy.TryAdd(hubMetadata.HubType.Name, AuthorizeHelper.BuildAuthorizePolicy(hubMetadata.HubType));
            }
            var authorizePolicy = _hubAuthorizePolicy[hubMetadata.HubType.Name];

            // Replaces the negotiate endpoint with one that does the service redirect
            var routeEndpointBuilder = new RouteEndpointBuilder(async context =>
            {
                await ServiceRouteHelper.RedirectToService(context, hubMetadata.HubType.Name, authorizePolicy);
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