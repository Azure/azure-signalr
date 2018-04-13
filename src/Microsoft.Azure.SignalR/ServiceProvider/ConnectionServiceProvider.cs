// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    public class ConnectionServiceProvider : IConnectionServiceProvider
    {
        private const string EndpointProperty = "endpoint";
        private const string AccessKeyProperty = "accesskey";
        private const int ClientPort = 5001;
        private const int ServerPort = 5002;
        internal static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromSeconds(30);

        private ConnectionService _connectionService;

        public ConnectionServiceProvider(IOptions<ServiceOptions> options)
        {
            string connectionString = null;
            if (String.IsNullOrEmpty(options.Value.ConnectionString))
            {
                connectionString = Environment.GetEnvironmentVariable(ServiceOptions.ConnectionStringDefaultKey);
            }
            else
            {
                connectionString = options.Value.ConnectionString;
            }
            if (String.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentNullException(nameof(ServiceOptions.ConnectionString));
            }
            _connectionService = ParseConnectionString(connectionString);
        }

        public string GetEndpoint()
        {
            return _connectionService.Endpoint;
        }

        public string GetAccessToken()
        {
            return _connectionService.AccessKey;
        }

        public string GenerateClientAccessToken<THub>(IEnumerable<Claim> claims = null, TimeSpan? lifetime = null) where THub : Hub
        {
            return GenerateClientAccessToken(typeof(THub).Name, claims, lifetime);
        }

        public string GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            var expire = lifetime.HasValue ? DateTime.UtcNow.Add(lifetime.Value) : DateTime.UtcNow.Add(DefaultAccessTokenLifetime);
            return AuthenticationHelper.GenerateJwtBearer(
                audience: GetClientEndpoint(hubName),
                claims: claims,
                expires: expire,
                signingKey: _connectionService.AccessKey
            );
        }

        public string GenerateServerAccessToken<THub>(TimeSpan? lifetime = null) where THub : Hub
        {
            return GenerateServerAccessToken(typeof(THub).Name, lifetime);
        }

        public string GenerateServerAccessToken(string hubName, TimeSpan? lifetime = null)
        {
            var expire = lifetime.HasValue ? DateTime.UtcNow.Add(lifetime.Value) : DateTime.UtcNow.Add(DefaultAccessTokenLifetime);
            return AuthenticationHelper.GenerateJwtBearer(
                audience: GetServerEndpoint(hubName),
                claims: null,
                expires: expire,
                signingKey: _connectionService.AccessKey
            );
        }

        public string GetClientEndpoint<THub>() where THub : Hub
        {
            return GetClientEndpoint(typeof(THub).Name);
        }

        public string GetClientEndpoint(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return InternalGetEndpoint(ClientPort, "client", hubName);
        }

        public string GetServerEndpoint<THub>() where THub : Hub
        {
            return GetServerEndpoint(typeof(THub).Name);
        }

        public string GetServerEndpoint(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return InternalGetEndpoint(ServerPort, "server", hubName);
        }

        private string InternalGetEndpoint(int port, string path, string hubName)
        {
            var endpoint = _connectionService.Endpoint;
            endpoint = endpoint.TrimEnd('/');
            return $"{endpoint}:{port}/{path}/?hub={hubName.ToLower()}";
        }

        private static ConnectionService ParseConnectionString(string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                var dict = connectionString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Split(new[] { '=' }, 2))
                    .ToDictionary(t => t[0].Trim().ToLower(), t => t[1].Trim(),
                        StringComparer.OrdinalIgnoreCase);
                if (dict.ContainsKey(EndpointProperty) && dict.ContainsKey(AccessKeyProperty))
                {
                    return new ConnectionService
                    {
                        Endpoint = dict[EndpointProperty],
                        AccessKey = dict[AccessKeyProperty]
                    };
                }
            }

            throw new ArgumentException($"Invalid Azure SignalR connection string: {connectionString}");
        }
    }

    public class ConnectionService
    {
        public string Endpoint { get; set; }

        public string AccessKey { get; set; }
    }
}
