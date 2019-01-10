// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// TODO: expose it to the customer
    /// </summary>
    internal class ServiceEndpoint
    {
        public string ConnectionString { get; }

        public EndpointType EndpointType { get; }

        public string Name { get; }

        internal string Endpoint { get; }

        internal string Version { get; }

        internal string AccessKey { get; }

        internal int? Port { get; }

        public ServiceEndpoint(string key, string connectionString) : this(connectionString)
        {
            if (!string.IsNullOrEmpty(key))
            {
                (Name, EndpointType) = ParseKey(key);
            }
        }

        public ServiceEndpoint(string connectionString, EndpointType type = EndpointType.Primary, string name = "")
        {
            // The provider is responsible to check if the connection string is empty and throw correct error message
            if (!string.IsNullOrEmpty(connectionString))
            {
                (Endpoint, AccessKey, Version, Port) = ConnectionStringParser.Parse(connectionString);
            }

            EndpointType = type;
            ConnectionString = connectionString;
            Name = name;
        }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(Name))
            {
                return Endpoint;
            }
            else
            {
                return $"[{Name}]{Endpoint}";
            }
        }

        public override int GetHashCode()
        {
            // We consider ServiceEndpoint with the same Endpoint (https://{signalr.endpoint}) as the unique identity
            return Endpoint?.GetHashCode() ?? 0;
        }

        internal static (string, EndpointType) ParseKey(string key)
        {
            if (key == Constants.ConnectionStringDefaultKey)
            {
                return (string.Empty, EndpointType.Primary);
            }

            if (key.StartsWith(Constants.ConnectionStringKeyPrefix))
            {
                // Azure:SignalR:ConnectionString:<name>:<type>
                var status = key.Substring(Constants.ConnectionStringKeyPrefix.Length);
                var parts = status.Split(':');
                if (parts.Length == 1)
                {
                    return (parts[0], EndpointType.Primary);
                }
                else
                {
                    if (Enum.TryParse<EndpointType>(parts[1], true, out var endpointStatus))
                    {
                        return (parts[0], endpointStatus);
                    }
                    else
                    {
                        return (status, EndpointType.Primary);
                    }
                }
            }

            throw new ArgumentException($"Invalid format: {key}", nameof(key));
        }
    }
}
