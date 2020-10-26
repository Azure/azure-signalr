// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR
{
    public class ServiceEndpoint
    {
        public string ConnectionString { get; }

        public EndpointType EndpointType { get; }

        public virtual string Name { get; internal set; }

        public string Endpoint { get; }

        internal string Version { get; }

        internal AccessKey AccessKey { get; private set; }

        internal int? Port { get; }

        /// <summary>
        /// When current app server instance has server connections connected to the target endpoint for current hub, it can deliver messages to that endpoint.
        /// The endpoint is then considered as *Online*; otherwise, *Offline*.
        /// Messages are not able to be delivered to an *Offline* endpoint.
        /// </summary>
        public bool Online { get; internal set; } = true;

        /// <summary>
        /// When the target endpoint has hub clients connected, the endpoint is considered as an *Active* endpoint.
        /// When the target endpoint has no hub clients connected for 10 minutes, the endpoint is considered as an *Inactive* one.
        /// User can choose to not send messages to an *Inactive* endpoint to save network traffic.
        /// But please note that as the *Active* status is reported to the server from remote service, there can be some delay when status changes.
        /// Don't rely on this status if you don't expect any message lose once a client is connected.
        /// </summary>
        public bool IsActive { get; internal set; } = true;

        /// <summary>
        /// Enriched endpoint metrics for customized routing.
        /// </summary>
        public EndpointMetrics EndpointMetrics { get; internal set; } = new EndpointMetrics();

        internal ServiceEndpoint(string endpoint, AuthOptions authOptions, int port = 443, EndpointType type = EndpointType.Primary)
        {
            Endpoint = endpoint;
            AccessKey = new AadAccessKey(authOptions);

            Version = "1.0";
            Port = port;
            Name = "";

            EndpointType = type;
        }

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
                string key;
                (Endpoint, key, Version, Port) = ConnectionStringParser.Parse(connectionString);
                AccessKey = new AccessKey(key);
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

        // test only
        internal ServiceEndpoint() { }

        internal void UpdateAccessKey(AccessKey key)
        {
            AccessKey = key;
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
            if (key == Constants.Keys.ConnectionStringDefaultKey || key == Constants.Keys.ConnectionStringSecondaryKey)
            {
                return (string.Empty, EndpointType.Primary);
            }

            if (key.StartsWith(Constants.Keys.ConnectionStringKeyPrefix))
            {
                // Azure:SignalR:ConnectionString:<name>:<type>
                return ParseKeyWithPrefix(key, Constants.Keys.ConnectionStringKeyPrefix);
            }

            if (key.StartsWith(Constants.Keys.ConnectionStringSecondaryKey))
            {
                return ParseKeyWithPrefix(key, Constants.Keys.ConnectionStringSecondaryKey);
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
