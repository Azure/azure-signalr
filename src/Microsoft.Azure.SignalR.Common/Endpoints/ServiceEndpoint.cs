// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;

namespace Microsoft.Azure.SignalR
{
    public class ServiceEndpoint
    {
        public string ConnectionString { get; }

        public EndpointType EndpointType { get; }

        public string Name { get; }

        /// <summary>
        /// Initial status as Online so that when the app server first starts, it can accept incoming negotiate requests, as for backward compatability
        /// </summary>
        public bool Online { get; internal set; } = true;

        public string Endpoint { get; }

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

        public ServiceEndpoint(ServiceEndpoint endpoint)
        {
            if (endpoint != null)
            {
                ConnectionString = endpoint.ConnectionString;
                EndpointType = endpoint.EndpointType;
                Name = endpoint.Name;
                Endpoint = endpoint.Endpoint;
                Version = endpoint.Version;
                AccessKey = endpoint.AccessKey;
                Port = endpoint.Port;
            }
        }

        public override string ToString()
        {
            var prefix = string.IsNullOrEmpty(Name) ? "" : $"[{Name}]";
            return $"{prefix}({EndpointType}){Endpoint}";
        }

        public override int GetHashCode()
        {
            // We consider ServiceEndpoint with the same Endpoint (https://{signalr.endpoint}) as the unique identity
            return (Endpoint, EndpointType, Name).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (!(obj is ServiceEndpoint that))
            {
                return false;
            }

            return Endpoint == that.Endpoint && EndpointType == that.EndpointType && Name == that.Name;
        }

        internal static (string, EndpointType) ParseKey(string key)
        {
            if (key == Constants.ConnectionStringDefaultKey || key == Constants.ConnectionStringSecondaryKey)
            {
                return (string.Empty, EndpointType.Primary);
            }

            if (key.StartsWith(Constants.ConnectionStringKeyPrefix))
            {
                // Azure:SignalR:ConnectionString:<name>:<type>
                return ParseKeyWithPrefix(key, Constants.ConnectionStringKeyPrefix);
            }

            if (key.StartsWith(Constants.ConnectionStringSecondaryKey))
            {
                return ParseKeyWithPrefix(key, Constants.ConnectionStringSecondaryKey);
            }

            throw new ArgumentException($"Invalid format: {key}", nameof(key));
        }

        private static (string, EndpointType) ParseKeyWithPrefix(string key, string prefix)
        {
            var status = key.Substring(prefix.Length);
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
    }
}
