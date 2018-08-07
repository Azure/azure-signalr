// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointUtility : IServiceEndpointUtility
    {
        private const string EndpointProperty = "endpoint";
        private const string AccessKeyProperty = "accesskey";
        private const string VersionProperty = "version";
        private const string PortProperty = "port";
        private const string PreviewVersion = "v1-preview";
        private const int PreviewClientPort = 5001;
        private const int PreviewServerPort = 5002;

        private static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {ServiceOptions.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IServiceCollection.AddAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private static readonly string MissingRequiredProperty =
            $"Connection string missing required properties {EndpointProperty} and {AccessKeyProperty}.";

        public ServiceEndpointUtility(IOptions<ServiceOptions> options)
        {
            var connectionString = options.Value.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(ConnectionStringNotFound);
            }

            AccessTokenLifetime = options.Value.AccessTokenLifetime;

            string version;
            (Endpoint, AccessKey, version, Port) = ParseConnectionString(connectionString);

            Version = NormalizeVersion(version);

            if (string.Equals(Version, PreviewVersion, StringComparison.Ordinal))
            {
                ClientPath = "client";
                ServerPath = "server";
                ClientAudiencePath = ":" + PreviewClientPort.ToString() + "/client";
                ServerAudiencePath = ":" + PreviewServerPort.ToString() + "/server";
                ClientPort = PreviewClientPort;
                ServerPort = PreviewServerPort;
            }
            else
            {
                ClientPath = Version + "/client";
                ServerPath = Version + "/server";
                ClientAudiencePath = "/" + Version + "/client";
                ServerAudiencePath = "/" + Version + "/server";
                ClientPort = Port;
                ServerPort = Port;
            }
        }

        public string Endpoint { get; }

        public string AccessKey { get; }

        public string Version { get; }

        public int? Port { get; }

        public string ClientPath { get; }

        public string ServerPath { get; }

        public string ClientAudiencePath { get; }

        public string ServerAudiencePath { get; }

        public int? ClientPort { get; }

        public int? ServerPort { get; }

        private TimeSpan AccessTokenLifetime { get; }

        public string GenerateClientAccessToken<THub>(IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
            where THub : Hub
        {
            return GenerateClientAccessToken(typeof(THub).Name, claims, lifetime);
        }

        public string GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null,
            TimeSpan? lifetime = null)
        {
            return InternalGenerateAccessToken(GetClientAudience(hubName), claims, lifetime ?? AccessTokenLifetime);
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
            return InternalGenerateAccessToken(GetServerAudience(hubName), claims, lifetime ?? AccessTokenLifetime);
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

            return InternalGetEndpoint(ClientPort, ClientPath, hubName);
        }

        public string GetClientAudience(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return InternalGetAudience(ClientAudiencePath, hubName);
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

            return InternalGetEndpoint(ServerPort, ServerPath, hubName);
        }

        public string GetServerAudience(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return InternalGetAudience(ServerAudiencePath, hubName);
        }

        private static string NormalizeVersion(string version)
        {
            if (version == null)
            {
                return PreviewVersion;
            }
            string previewPostfix = "-preview";
            if (version.EndsWith(previewPostfix))
            {
                version = version.Remove(version.Length - previewPostfix.Length);
            }
            else
            {
                previewPostfix = string.Empty;
            }
            if (version.EndsWith(".0"))
            {
                return "v" + version.Remove(version.Length - 2) + previewPostfix;
            }
            return "v" + version + previewPostfix;
        }

        private string InternalGetEndpoint(int? port, string path, string hubName)
        {
            if (port == null)
            {
                return $"{Endpoint}/{path}/?hub={hubName.ToLower()}";
            }
            return $"{Endpoint}:{port}/{path}/?hub={hubName.ToLower()}";
        }

        private string InternalGetAudience(string path, string hubName)
        {
            return $"{Endpoint}{path}/?hub={hubName.ToLower()}";
        }

        private string InternalGenerateAccessToken(string audience, IEnumerable<Claim> claims, TimeSpan lifetime)
        {
            var expire = DateTime.UtcNow.Add(lifetime);

            return AuthenticationHelper.GenerateJwtBearer(
                audience: audience,
                claims: claims,
                expires: expire,
                signingKey: AccessKey
            );
        }

        private static readonly char[] PropertySeparator = { ';' };
        private static readonly char[] KeyValueSeparator = { '=' };

        internal static (string endpoint, string accessKey, string version, int? port) ParseConnectionString(string connectionString)
        {
            var properties = connectionString.Split(PropertySeparator, StringSplitOptions.RemoveEmptyEntries);
            if (properties.Length > 1)
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in properties)
                {
                    var kvp = property.Split(KeyValueSeparator, 2);
                    if (kvp.Length != 2) continue;

                    var key = kvp[0].Trim();
                    if (dict.ContainsKey(key))
                    {
                        throw new ArgumentException($"Duplicate properties found in connection string: {key}.");
                    }

                    dict.Add(key, kvp[1].Trim());
                }

                if (dict.ContainsKey(EndpointProperty) && dict.ContainsKey(AccessKeyProperty))
                {
                    if (!ValidateEndpoint(dict[EndpointProperty]))
                    {
                        throw new ArgumentException($"Endpoint property in connection string is not a valid URI: {dict[EndpointProperty]}.");
                    }

                    var version = dict.TryGetValue(VersionProperty, out var v) ? v : null;
                    int? port = null;
                    if (dict.TryGetValue(PortProperty, out var ps) &&
                        int.TryParse(ps, out var p) &&
                        p > 0 && p < 65535)
                    {
                        port = p;
                    }
                    return (dict[EndpointProperty].TrimEnd('/'), dict[AccessKeyProperty], version, port);
                }
            }

            throw new ArgumentException(MissingRequiredProperty);
        }

        internal static bool ValidateEndpoint(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }
}
