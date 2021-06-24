// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Azure.Identity;

namespace Microsoft.Azure.SignalR
{
    internal static class ConnectionStringParser
    {
        private const string AccessKeyProperty = "accesskey";
        private const string AuthTypeProperty = "authtype";
        private const string ClientCertProperty = "clientCert";
        private const string ClientEndpointProperty = "ClientEndpoint";
        private const string ClientIdProperty = "clientId";
        private const string ClientSecretProperty = "clientSecret";
        private const string EndpointProperty = "endpoint";
        private const string InvalidVersionValueFormat = "Version {0} is not supported.";
        private const string PortProperty = "port";
        // For SDK 1.x, only support Azure SignalR Service 1.x
        private const string SupportedVersion = "1";

        private const string TenantIdProperty = "tenantId";
        private const string ValidVersionRegex = "^" + SupportedVersion + @"\.\d+(?:[\w-.]+)?$";
        private const string VersionProperty = "version";
        private static readonly string InvalidPortValue = $"Invalid value for {PortProperty} property.";

        private static readonly char[] KeyValueSeparator = { '=' };

        private static readonly string MissingAccessKeyProperty =
            $"{AccessKeyProperty} is required.";

        private static readonly string MissingClientSecretProperty =
            $"Connection string missing required properties {ClientSecretProperty} or {ClientCertProperty}.";

        private static readonly string MissingEndpointProperty =
                                            $"Connection string missing required properties {EndpointProperty}.";

        private static readonly string MissingTenantIdProperty =
            $"Connection string missing required properties {TenantIdProperty}.";
        private static readonly char[] PropertySeparator = { ';' };

        internal static (AccessKey accessKey, string version, string clientEndpoint) Parse(string connectionString)
        {
            var properties = connectionString.Split(PropertySeparator, StringSplitOptions.RemoveEmptyEntries);
            if (properties.Length < 2)
            {
                throw new ArgumentException(MissingEndpointProperty, nameof(connectionString));
            }

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

            // parse and validate endpoint.
            if (!dict.TryGetValue(EndpointProperty, out var endpoint))
            {
                throw new ArgumentException(MissingEndpointProperty, nameof(connectionString));
            }
            endpoint = endpoint.TrimEnd('/');

            if (!ValidateEndpoint(endpoint))
            {
                throw new ArgumentException($"Endpoint property in connection string is not a valid URI: {dict[EndpointProperty]}.");
            }

            // parse and validate version.
            string version = null;
            if (dict.TryGetValue(VersionProperty, out var v))
            {
                if (!Regex.IsMatch(v, ValidVersionRegex))
                {
                    throw new ArgumentException(string.Format(InvalidVersionValueFormat, v), nameof(connectionString));
                }
                version = v;
            }

            // parse and validate port.
            int? port = null;
            if (dict.TryGetValue(PortProperty, out var s))
            {
                if (int.TryParse(s, out var p) && p > 0 && p <= 0xFFFF)
                {
                    port = p;
                }
                else
                {
                    throw new ArgumentException(InvalidPortValue, nameof(connectionString));
                }
            }

            // parse and validate clientEndpoint.
            if (dict.TryGetValue(ClientEndpointProperty, out var clientEndpoint))
            {
                if (!ValidateEndpoint(clientEndpoint))
                {
                    throw new ArgumentException($"{ClientEndpointProperty} property in connection string is not a valid URI: {clientEndpoint}.");
                }
            }

            dict.TryGetValue(AuthTypeProperty, out string type);
            AccessKey accessKey = type?.ToLower() switch
            {
                "aad" => BuildAadAccessKey(dict, endpoint, port),
                _ => BuildAccessKey(dict, endpoint, port),
            };
            return (accessKey, version, clientEndpoint);
        }

        internal static bool ValidateEndpoint(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        private static AccessKey BuildAadAccessKey(Dictionary<string, string> dict, string endpoint, int? port)
        {
            if (dict.TryGetValue(ClientIdProperty, out var clientId))
            {
                if (dict.TryGetValue(TenantIdProperty, out var tenantId))
                {
                    if (dict.TryGetValue(ClientSecretProperty, out var clientSecret))
                    {
                        return new AadAccessKey(new ClientSecretCredential(tenantId, clientId, clientSecret), endpoint, port);
                    }
                    else if (dict.TryGetValue(ClientCertProperty, out var clientCertPath))
                    {
                        return new AadAccessKey(new ClientCertificateCredential(tenantId, clientId, clientCertPath), endpoint, port);
                    }
                    else
                    {
                        throw new ArgumentException(MissingClientSecretProperty, ClientSecretProperty);
                    }
                }
                else
                {
                    return new AadAccessKey(new ManagedIdentityCredential(clientId), endpoint, port);
                }
            }
            else
            {
                return new AadAccessKey(new ManagedIdentityCredential(), endpoint, port);
            }
        }

        private static AccessKey BuildAccessKey(Dictionary<string, string> dict, string endpoint, int? port)
        {
            if (dict.TryGetValue(AccessKeyProperty, out var key))
            {
                return new AccessKey(key, endpoint, port);
            }
            throw new ArgumentException(MissingAccessKeyProperty, AccessKeyProperty);
        }
    }
}