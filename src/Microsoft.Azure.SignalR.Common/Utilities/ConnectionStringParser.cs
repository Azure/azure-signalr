// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.SignalR
{
    internal static class ConnectionStringParser
    {
        private const string AuthTypeProperty = "authtype";
        private const string ClientIdProperty = "clientId";
        private const string ClientSecretProperty = "clientSecret";
        private const string ClientCertProperty = "clientCert";
        private const string TenantIdProperty = "tenantId";
        private const string EndpointProperty = "endpoint";
        private const string AccessKeyProperty = "accesskey";
        private const string VersionProperty = "version";
        private const string PortProperty = "port";
        private const string ClientEndpointProperty = "ClientEndpoint";

        // For SDK 1.x, only support Azure SignalR Service 1.x
        private const string SupportedVersion = "1";
        private const string ValidVersionRegex = "^" + SupportedVersion + @"\.\d+(?:[\w-.]+)?$";

        private static readonly string MissingEndpointProperty =
            $"Connection string missing required properties {EndpointProperty}.";

        private static readonly string MissingTenantIdProperty =
            $"Connection string missing required properties {TenantIdProperty}.";

        private static readonly string MissingClientSecretProperty =
            $"Connection string missing required properties {ClientSecretProperty} or {ClientCertProperty}.";

        private static readonly string MissingAccessKeyProperty =
            $"{AccessKeyProperty} is required.";

        private static readonly string FileNotExists = "The given filepath is not a valid cert file.";

        private const string InvalidVersionValueFormat = "Version {0} is not supported.";

        private static readonly string InvalidPortValue = $"Invalid value for {PortProperty} property.";

        private static readonly char[] PropertySeparator = { ';' };
        private static readonly char[] KeyValueSeparator = { '=' };

        internal static (string endpoint, AccessKey accessKey, string version, int? port, string clientEndpoint) Parse(string connectionString)
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

            if (!dict.ContainsKey(EndpointProperty))
            {
                throw new ArgumentException(MissingEndpointProperty, nameof(connectionString));
            }

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

            if (dict.TryGetValue(ClientEndpointProperty, out var clientEndpoint))
            {
                if (!ValidateEndpoint(clientEndpoint))
                {
                    throw new ArgumentException($"{ClientEndpointProperty} property in connection string is not a valid URI: {clientEndpoint}.");
                }
            }

            AccessKey accessKey;
            if (dict.ContainsKey(AuthTypeProperty) && "aad".Equals(dict[AuthTypeProperty], StringComparison.OrdinalIgnoreCase))
            {
                if (dict.ContainsKey(ClientIdProperty))
                {
                    if (!dict.ContainsKey(TenantIdProperty))
                    {
                        throw new ArgumentNullException(MissingTenantIdProperty, nameof(connectionString));
                    }

                    var options = new AadApplicationOptions(dict[ClientIdProperty], dict[TenantIdProperty]);
                    if (dict.ContainsKey(ClientSecretProperty))
                    {
                        accessKey = new AadAccessKey(options.WithClientSecret(dict[ClientSecretProperty]));
                    }
                    else if (dict.ContainsKey(ClientCertProperty))
                    {
                        if (!File.Exists(dict[ClientCertProperty]))
                        {
                            throw new ArgumentNullException(FileNotExists, nameof(connectionString));
                        }
                        var cert = new X509Certificate2(dict[ClientCertProperty]);
                        accessKey = new AadAccessKey(options.WithClientCert(cert));
                    }
                    else
                    {
                        throw new ArgumentNullException(MissingClientSecretProperty, nameof(connectionString));
                    }
                }
                else
                {
                    accessKey = new AadAccessKey(new AadManagedIdentityOptions());
                }
            }
            else if (dict.ContainsKey(AccessKeyProperty))
            {
                accessKey = new AccessKey(dict[AccessKeyProperty]);
            }
            else
            {
                throw new ArgumentNullException(MissingAccessKeyProperty, nameof(connectionString));
            }

            return (dict[EndpointProperty].TrimEnd('/'), accessKey, version, port, clientEndpoint);
        }

        internal static bool ValidateEndpoint(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }
}