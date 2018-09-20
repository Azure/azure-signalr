// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Azure.SignalR
{
    internal static class ConnectionStringParser
    {
        private const string EndpointProperty = "endpoint";
        private const string AccessKeyProperty = "accesskey";
        private const string VersionProperty = "version";
        private const string PortProperty = "port";
        // For SDK 1.x, only support Azure SignalR Service 1.x
        private const string SupportedVersion = "1";
        private const string ValidVersionRegex = "^" + SupportedVersion + @"\.\d+(?:[\w-.]+)?$";

        private static readonly string MissingRequiredProperty =
            $"Connection string missing required properties {EndpointProperty} and {AccessKeyProperty}.";

        private const string InvalidVersionValueFormat = "Version {0} is not supported.";

        private static readonly string InvalidPortValue = $"Invalid value for {PortProperty} property.";

        private static readonly char[] PropertySeparator = { ';' };
        private static readonly char[] KeyValueSeparator = { '=' };

        internal static (string endpoint, string accessKey, string version, int? port) Parse(string connectionString)
        {
            var properties = connectionString.Split(PropertySeparator, StringSplitOptions.RemoveEmptyEntries);
            if (properties.Length < 2)
            {
                throw new ArgumentException(MissingRequiredProperty, nameof(connectionString));
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

            if (!dict.ContainsKey(EndpointProperty) || !dict.ContainsKey(AccessKeyProperty))
            {
                throw new ArgumentException(MissingRequiredProperty, nameof(connectionString));
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

            return (dict[EndpointProperty].TrimEnd('/'), dict[AccessKeyProperty], version, port);
        }

        internal static bool ValidateEndpoint(string endpoint)
        {
            return Uri.TryCreate(endpoint, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }
}