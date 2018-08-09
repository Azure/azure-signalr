// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.RegularExpressions;
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
        private const string PreviewVersion = "1.0-preview";
        private const string SupportedVersion = "1";
        private const string ValidVersionRegex = "^" + SupportedVersion + @"\.\d+(?:[\w-.]+)?$";

        private static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {ServiceOptions.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IServiceCollection.AddAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private static readonly string MissingRequiredProperty =
            $"Connection string missing required properties {EndpointProperty} and {AccessKeyProperty}.";
        private static readonly string InvalidVersionValue =
            $"Invalid value for {VersionProperty} property, value must follow regex: {ValidVersionRegex}.";
        private static readonly string InvalidPortValue =
            $"Invalid value for {PortProperty} property.";

        private readonly IServiceEndpointGenerator _generator;

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

            Version = version ?? PreviewVersion;

            if (Version == PreviewVersion)
            {
                _generator = new PreviewServiceEndpointGenerator(Endpoint, AccessKey);
            }
            else
            {
                _generator = new DefaultServiceEndpointGenerator(Endpoint, AccessKey, Version, Port);
            }
        }

        public string Endpoint { get; }

        public string AccessKey { get; }

        public string Version { get; }

        public int? Port { get; }

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

        public string GetClientEndpoint<THub>() where THub : Hub =>
            GetClientEndpoint(typeof(THub).Name);

        public string GetClientEndpoint(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetClientEndpoint(hubName);
        }

        public string GetClientAudience(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetClientAudience(hubName);
        }

        public string GetServerEndpoint<THub>() where THub : Hub =>
            GetServerEndpoint(typeof(THub).Name);

        public string GetServerEndpoint(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetServerEndpoint(hubName);
        }

        public string GetServerAudience(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetServerAudience(hubName);
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

                    string version = null;
                    if (dict.TryGetValue(VersionProperty, out var v))
                    {
                        if (Regex.IsMatch(v, ValidVersionRegex))
                        {
                            version = v;
                        }
                        else
                        {
                            throw new ArgumentException(InvalidVersionValue, nameof(connectionString));
                        }
                    }
                    int? port = null;
                    if (dict.TryGetValue(PortProperty, out var s))
                    {
                        if (int.TryParse(s, out var p) &&
                            p > 0 && p <= 0xFFFF)
                        {
                            port = p;
                        }
                        else
                        {
                            throw new ArgumentException(InvalidPortValue, nameof(connectionString));
                        }
                    }
                    return (dict[EndpointProperty].TrimEnd('/'), dict[AccessKeyProperty], version, port);
                }
            }

            throw new ArgumentException(MissingRequiredProperty, nameof(connectionString));
        }

        internal static bool ValidateEndpoint(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }
}
