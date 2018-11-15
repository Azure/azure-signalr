// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceEndpointProvider
    {
        string GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null);

        string GetClientEndpoint(string hubName, string originalPath, string queryString);

        string GenerateServerAccessToken<THub>(string userId, TimeSpan? lifetime = null) where THub : Hub;

        string GetServerEndpoint<THub>() where THub : Hub;
    }

    public enum ServerStatus
    {
        New,
        Online,
        Offline,
    }

    internal interface IEndpointRouter
    {
        IServiceEndpointProvider GetNegotiateEndpoint(HttpContext context);

        IEnumerable<IServiceEndpointProvider> GetServiceConnectionEndpoints();
    }

    internal class EndpointRouter : IEndpointRouter
    {
        private ConnectionEndpoint[] _availableEndpoints;

        // active ones
        private ConnectionEndpoint[] _activeEndpoints;

        // disabled ones
        // /negotiate => not return
        // server-connection connected
        // /OnlineServerAsync => server-connection disconnected
        private ConnectionEndpoint[] _enabledEndpoints;

        // new ones
        // /negotiate => not return
        // server-connection connected
        // /OnlineServerAsync => /negotiate return
        private ConnectionEndpoint[] _newEndpoints;

        private TimeSpan _accessTokenLifetime;

        private Func<ConnectionEndpoint[], ConnectionEndpoint> _router;

        private ServiceOptions _options;
        public EndpointRouter(IOptions<ServiceOptions> options)
        {
            _options = options.Value;
            var endpoints = options.Value.ConnectionStrings;
            _accessTokenLifetime = options.Value.AccessTokenLifetime;
            _router = options.Value.EndpointRouter;

            _availableEndpoints = endpoints;
            _activeEndpoints = endpoints.Where(s => s.Status == EndpointStatus.Active).ToArray();
            _enabledEndpoints = endpoints.Where(s => s.Status != EndpointStatus.Disabled).ToArray();
            _newEndpoints = endpoints.Where(s => s.Status == EndpointStatus.New).ToArray();
        }

        public IServiceEndpointProvider GetNegotiateEndpoint(HttpContext context)
        {
            // For newly added servers, "new" ones should not be returned when /negotiate
            // Until ServerStatus is switched to "ready"
            if (GlobalStatus.ServerStatus == ServerStatus.New)
            {
                return SelectProvider(_activeEndpoints);
            }
            else if (GlobalStatus.ServerStatus == ServerStatus.Online)
            {
                // When it's online, route from not-disabled ones
                return SelectProvider(_enabledEndpoints);
            }
            else
            {
                // when it is offline, need more design
                throw new NotImplementedException();
            }
        }

        // select with affinity?
        private IServiceEndpointProvider SelectProvider(ConnectionEndpoint[] connections)
        {
            ConnectionEndpoint connection = _router != null ? _router(connections) : GetDefaultRouter(connections);
            return GetProvider(connection);
        }

        private ConnectionEndpoint GetDefaultRouter(ConnectionEndpoint[] connections)
        {
            if (connections == null || connections.Length == 0)
            {
                return null;
            }

            return connections[StaticRandom.Next(connections.Length - 1)];
        }

        public IEnumerable<IServiceEndpointProvider> GetServiceConnectionEndpoints()
        {
            IEnumerable<ConnectionEndpoint> routers = _options.ConnectServiceStrategy == null ? _availableEndpoints : _options.ConnectServiceStrategy(_availableEndpoints);

            return routers.Select(GetProvider);
        }

        private IServiceEndpointProvider GetProvider(ConnectionEndpoint endpoint)
        {
            return new ServiceEndpointProvider(endpoint.Endpoint, _accessTokenLifetime);
        }
    }

    public static class GlobalStatus
    {
        public static ServerStatus ServerStatus = ServerStatus.New;
    }
}
