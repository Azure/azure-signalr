// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET11_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// Intercepts ASP.NET Core's /refresh endpoint for ASRS-connected hubs and routes it through the Azure SignalR refresh flow.
    /// Detects endpoints carrying both <see cref="AuthenticationRefreshMetadata"/> and <see cref="HubMetadata"/>
    /// </summary>
    internal class AuthRefreshMatcherPolicy : MatcherPolicy, IEndpointSelectorPolicy
    {
        private static readonly MethodInfo _createRefreshEndpointCoreMethodInfo = typeof(AuthRefreshMatcherPolicy).GetMethod(nameof(CreateRefreshEndpointCore), BindingFlags.NonPublic | BindingFlags.Static);

        private readonly ConcurrentDictionary<Type, Endpoint> _refreshEndpointCache = new ConcurrentDictionary<Type, Endpoint>();

        public override int Order => 1;

        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
        {
            foreach (var endpoint in endpoints)
            {
                var hubMetadata = endpoint.Metadata.GetMetadata<HubMetadata>();
                var refreshMetadata = endpoint.Metadata.GetMetadata<AuthenticationRefreshMetadata>();

                if (hubMetadata != null && refreshMetadata != null)
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
                if (candidate.Endpoint is RouteEndpoint routeEndpoint)
                {
                    var refreshMetadata = routeEndpoint.Metadata.GetMetadata<AuthenticationRefreshMetadata>();
                    var hubMetadata = routeEndpoint.Metadata.GetMetadata<HubMetadata>();
                    // Only replace the refresh endpoint, leaving negotiate/connect endpoints untouched.
                    if (refreshMetadata != null && hubMetadata != null)
                    {
                        var newEndpoint = _refreshEndpointCache.GetOrAdd(hubMetadata.HubType, CreateRefreshEndpoint(hubMetadata.HubType, routeEndpoint));

                        candidates.ReplaceEndpoint(i, newEndpoint, candidate.Values);
                    }
                }
            }

            return Task.CompletedTask;
        }

        private Func<Type, Endpoint> CreateRefreshEndpoint(Type hubType, RouteEndpoint routeEndpoint)
        {
            var genericMethodInfo = _createRefreshEndpointCoreMethodInfo.MakeGenericMethod(hubType);
            return type =>
            {
                return (Endpoint)genericMethodInfo.Invoke(this, new object[] { routeEndpoint });
            };
        }

        private static Endpoint CreateRefreshEndpointCore<THub>(RouteEndpoint routeEndpoint) where THub : Hub
        {
            var routeEndpointBuilder = new RouteEndpointBuilder(async context =>
            {
                await ServiceRouteHelper.RefreshToService<THub>(context);
            },
            routeEndpoint.RoutePattern,
            routeEndpoint.Order);

            routeEndpointBuilder.DisplayName = routeEndpoint.DisplayName;

            foreach (var metadata in routeEndpoint.Metadata)
            {
                routeEndpointBuilder.Metadata.Add(metadata);
            }

            return routeEndpointBuilder.Build();
        }
    }
}
#endif
