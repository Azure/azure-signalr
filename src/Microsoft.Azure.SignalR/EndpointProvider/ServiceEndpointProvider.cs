// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointProvider : IServiceEndpointProvider
    {
        private const string EndpointProperty = "endpoint";
        private const string AccessKeyProperty = "accesskey";
        private const string VersionProperty = "version";
        private const string PortProperty = "port";
        private const string PreviewVersion = "1.0-preview";
        // For SDK 1.x, only support Azure SignalR Service 1.x
        private const string SupportedVersion = "1";
        private const string ValidVersionRegex = "^" + SupportedVersion + @"\.\d+(?:[\w-.]+)?$";

        private static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {ServiceOptions.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IServiceCollection.AddAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private static readonly string MissingRequiredProperty =
            $"Connection string missing required properties {EndpointProperty} and {AccessKeyProperty}.";
        private static readonly string InvalidVersionValueFormat =
            "Version {0} is not supported.";
        private static readonly string InvalidPortValue =
            $"Invalid value for {PortProperty} property.";

        private readonly string _endpoint;
        private readonly string _accessKey;
        private readonly TimeSpan _accessTokenLifetime;
        private readonly IServiceEndpointGenerator _generator;

        public ServiceEndpointProvider(IOptions<ServiceOptions> options)
        {
            var connectionString = options.Value.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new ArgumentException(ConnectionStringNotFound);
            }

            _accessTokenLifetime = options.Value.AccessTokenLifetime;

            string version;
            int? port;
            (_endpoint, _accessKey, version, port) = ParseConnectionString(connectionString);

            if (version == null || version == PreviewVersion)
            {
                _generator = new PreviewServiceEndpointGenerator(_endpoint, _accessKey);
            }
            else
            {
                _generator = new DefaultServiceEndpointGenerator(_endpoint, _accessKey, version, port);
            }
        }

        public string GenerateClientAccessToken(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var audience = _generator.GetClientAudience(hubName);

            return InternalGenerateAccessToken(audience, claims, lifetime ?? _accessTokenLifetime);
        }

        public string GenerateServerAccessToken<THub>(string userId, TimeSpan? lifetime = null) where THub : Hub
        {
            var audience = _generator.GetServerAudience(typeof(THub).Name);
            var claims = userId != null ? new[] {new Claim(ClaimTypes.NameIdentifier, userId)} : null;

            return InternalGenerateAccessToken(audience, claims, lifetime ?? _accessTokenLifetime);
        }

        public string GetClientEndpoint(string hubName, QueryString queryString)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var endpoint = _generator.GetClientEndpoint(hubName);

            if (queryString == QueryString.Empty)
            {
                return endpoint;
            }

            return endpoint.Contains("?") 
                ? $"{endpoint}&{queryString.Value.Substring(1)}"
                : $"{endpoint}{queryString}";
        }

        public string GetServerEndpoint<THub>() where THub : Hub
        {
            return _generator.GetServerEndpoint(typeof(THub).Name);
        }

        private string InternalGenerateAccessToken(string audience, IEnumerable<Claim> claims, TimeSpan lifetime)
        {
            var expire = DateTime.UtcNow.Add(lifetime);

            return AuthenticationHelper.GenerateJwtBearer(
                audience: audience,
                claims: claims,
                expires: expire,
                signingKey: _accessKey
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
                            throw new ArgumentException(string.Format(InvalidVersionValueFormat, v), nameof(connectionString));
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
