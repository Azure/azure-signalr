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
    internal class ServiceEndpointUtility : IServiceEndpointUtility
    {
        private const string EndpointProperty = "endpoint";
        private const string AccessKeyProperty = "accesskey";
        private const int ClientPort = 5001;
        private const int ServerPort = 5002;
        internal static readonly TimeSpan DefaultAccessTokenLifetime = TimeSpan.FromSeconds(30);

        public ServiceEndpointUtility(IOptions<ServiceOptions> options)
        {
            var connectionString = options.Value.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(
                    $"No connection string was specified. Please specify a configuration entry for {ServiceOptions.ConnectionStringDefaultKey} or explicitly pass one using IServiceCollection.AddSignalRService(connectionString) in Startup.ConfigureServices.");
            }

            (Endpoint, AccessKey) = ParseConnectionString(connectionString);
        }

        public string Endpoint { get; }

        public string AccessKey { get; }

        public string GenerateClientAccessToken<THub>(IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
            where THub : Hub
        {
            return GenerateClientAccessToken(typeof(THub).Name, claims, lifetime);
        }

        public string GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null,
            TimeSpan? lifetime = null)
        {
            return InternalGenerateAccessToken(GetClientEndpoint(hubName), claims, lifetime);
        }

        public string GenerateServerAccessToken<THub>(string userId, TimeSpan? lifetime = null) where THub : Hub
        {
            return GenerateServerAccessToken(typeof(THub).Name, userId, lifetime);
        }

        public string GenerateServerAccessToken(string hubName, string userId, TimeSpan? lifetime = null)
        {
            IEnumerable<Claim> claims = null;
            if (userId != null)
            {
                claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                };
            }
            return InternalGenerateAccessToken(GetServerEndpoint(hubName), claims, lifetime);
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
            return $"{Endpoint}:{port}/{path}/?hub={hubName.ToLower()}";
        }

        private string InternalGenerateAccessToken(string audience, IEnumerable<Claim> claims, TimeSpan? lifetime)
        {
            var expire = lifetime.HasValue
                ? DateTime.UtcNow.Add(lifetime.Value)
                : DateTime.UtcNow.Add(DefaultAccessTokenLifetime);
            return AuthenticationHelper.GenerateJwtBearer(
                audience: audience,
                claims: claims,
                expires: expire,
                signingKey: AccessKey
            );
        }

        private static (string, string) ParseConnectionString(string connectionString)
        {
            var dict = connectionString.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Split(new[] {'='}, 2))
                .ToDictionary(t => t[0].Trim().ToLower(), t => t[1].Trim(),
                    StringComparer.OrdinalIgnoreCase);
            if (dict.ContainsKey(EndpointProperty) && dict.ContainsKey(AccessKeyProperty))
            {
                return (dict[EndpointProperty].TrimEnd('/'), dict[AccessKeyProperty]);
            }

            throw new ArgumentException("");
        }
    }
}
