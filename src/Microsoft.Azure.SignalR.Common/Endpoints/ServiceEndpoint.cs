// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;

namespace Microsoft.Azure.SignalR
{
    public class ServiceEndpoint
    {
        public string ConnectionString { get; }

        public EndpointType EndpointType { get; }

        public string Name { get; }

        /// <summary>
        /// Connection == null means no service connection to this endpoint is yet initialized
        /// When no connection is yet initialized, we consider the endpoint as Online for now, for compatable with current /negotiate behavior
        /// TODO: improve /negotiate behavior when the server-connection is being established
        /// </summary>
        public bool Online => Connection == null || Connection.Status == ServiceConnectionStatus.Connected;

        public string Endpoint { get; }

        internal string Version { get; }

        internal string AccessKey { get; }

        internal int? Port { get; }

        internal IServiceConnectionContainer Connection { get; set; }

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
            ConnectionString = endpoint.ConnectionString;
            EndpointType = endpoint.EndpointType;
            Name = endpoint.Name;
            Endpoint = endpoint.Endpoint;
            Version = endpoint.Version;
            AccessKey = endpoint.AccessKey;
            Port = endpoint.Port;
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

            return Endpoint == that.Endpoint;
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
