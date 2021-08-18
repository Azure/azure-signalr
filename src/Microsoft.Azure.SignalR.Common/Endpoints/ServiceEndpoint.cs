// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Azure.Core;

namespace Microsoft.Azure.SignalR
{
    public class ServiceEndpoint
    {
        public string ConnectionString { get; }

        public EndpointType EndpointType { get; } = EndpointType.Primary;

        public virtual string Name { get; internal set; } = "";

        public string Endpoint => AccessKey?.Endpoint;

        internal int? Port => AccessKey?.Port;

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

        /// <summary>
        /// The customized endpoint that the client will be redirected to
        /// </summary>
        internal string ClientEndpoint { get; }

        internal string Version { get; }

        internal AccessKey AccessKey { get; private set; }

        /// <summary>
        /// Connection string constructor with dict key
        /// </summary>
        /// <param name="nameWithEndpointType"></param>
        /// <param name="connectionString"></param>
        public ServiceEndpoint(string nameWithEndpointType, string connectionString) : this(connectionString)
        {
            (Name, EndpointType) = Parse(nameWithEndpointType);
        }

        /// <summary>
        /// Connection string constructor
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="type"></param>
        /// <param name="name"></param>
        public ServiceEndpoint(string connectionString, EndpointType type = EndpointType.Primary, string name = "")
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException($"'{nameof(connectionString)}' cannot be null or whitespace", nameof(connectionString));
            }

            (AccessKey, Version, ClientEndpoint) = ConnectionStringParser.Parse(connectionString);

            EndpointType = type;
            ConnectionString = connectionString;
            Name = name;
        }

        /// <summary>
        /// Azure active directory constructor with dict key
        /// </summary>
        /// <param name="nameWithEndpointType"></param>
        /// <param name="endpoint"></param>
        /// <param name="credential"></param>
        public ServiceEndpoint(string nameWithEndpointType, Uri endpoint, TokenCredential credential) : this(endpoint, credential)
        {
            (Name, EndpointType) = Parse(nameWithEndpointType);
        }

        /// <summary>
        /// Azure active directory constructor
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="credential"></param>
        /// <param name="endpointType"></param>
        /// <param name="name"></param>
        public ServiceEndpoint(Uri endpoint, TokenCredential credential, EndpointType endpointType = EndpointType.Primary, string name = "")
        {
            if (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("Endpoint scheme must be 'http://' or 'https://'");
            }
            AccessKey = new AadAccessKey(credential, $"{endpoint.Scheme}://{endpoint.Host}", endpoint.Port);
            (Name, EndpointType) = (name, endpointType);
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other"></param>
        public ServiceEndpoint(ServiceEndpoint other)
        {
            if (other != null)
            {
                ConnectionString = other.ConnectionString;
                EndpointType = other.EndpointType;
                Name = other.Name;
                Version = other.Version;
                AccessKey = other.AccessKey;
                ClientEndpoint = other.ClientEndpoint;
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

        private static (string, EndpointType) Parse(string nameWithEndpointType)
        {
            if (string.IsNullOrEmpty(nameWithEndpointType))
            {
                return (string.Empty, EndpointType.Primary);
            }

            var parts = nameWithEndpointType.Split(':');
            if (parts.Length == 1)
            {
                return (parts[0], EndpointType.Primary);
            }
            else if (Enum.TryParse<EndpointType>(parts[1], true, out var endpointStatus))
            {
                return (parts[0], endpointStatus);
            }
            else
            {
                return (nameWithEndpointType, EndpointType.Primary);
            }
        }
    }
}