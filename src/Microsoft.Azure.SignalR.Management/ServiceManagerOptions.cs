// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Configurable options for Azure SignalR Management SDK.
    /// </summary>
    public class ServiceManagerOptions
    {
        /// <summary>
        /// Gets or sets the ApplicationName which will be prefixed to each hub name
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Gets or sets the total number of connections from SDK to Azure SignalR Service. Default value is 1.
        /// </summary>
        public int ConnectionCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets the connection string of Azure SignalR Service instance and switches to single-endpoint.
        /// </summary>
        public string ConnectionString
        {
            get => _connectionString;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"'{nameof(ConnectionString)}' cannot be null or whitespace", nameof(ConnectionString));
                }
                else
                {
                    ServiceEndpoint = new ServiceEndpoint(value);
                    _connectionString = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the proxy used when ServiceManager will attempt to connect to Azure SignalR Service.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets the service endpoint for accessing Azure SignalR Service and switches to single-endpoint mode.
        /// </summary>
        public ServiceEndpoint ServiceEndpoint
        {
            get => _serviceEndpoint;
            set
            {
                _multiEndpointState = false;
                _serviceEndpoint = value;
            }
        }

        /// <summary>
        /// Gets or sets the service endpoints for accessing Azure SignalR Service and switches to multi-endpoint mode.
        /// </summary>
        internal ServiceEndpoint[] ServiceEndpoints  //not ready for public use
        {
            get => _serviceEndpoints;
            set
            {
                if (value == null)//enable user to reset the _multiEndpointState
                {
                    _serviceEndpoints = null;
                    return;
                }
                if (value.Length == 0)
                {
                    throw new ArgumentException("collection is empty", nameof(ServiceEndpoints));
                }

                _serviceEndpoints = value;
                _multiEndpointState = true;
            }
        }

        /// <summary>
        /// Gets or sets the transport type to Azure SignalR Service. Default value is Transient.
        /// </summary>
        public ServiceTransportType ServiceTransportType { get; set; } = ServiceTransportType.Transient;

        private bool _multiEndpointState;

        private string _connectionString;
        private ServiceEndpoint _serviceEndpoint;
        private ServiceEndpoint[] _serviceEndpoints;

        public bool InMultiEndpointState()
        {
            ValidateOptions();
            return _multiEndpointState;
        }

        private void ValidateOptions()
        {
            ValidateServiceEndpoint();
            ValidateServiceTransportType();
        }

        private void ValidateServiceEndpoint()
        {
            if (ServiceEndpoint == null && ServiceEndpoints == null)
            {
                throw new InvalidOperationException($"service endpoint(s) not configured. Please set one of the following properties {nameof(ConnectionString)}, {nameof(ServiceEndpoint)}, {nameof(ServiceEndpoints)}");
            }
            if (ServiceEndpoint != null && ServiceEndpoints != null)
            {
                throw new InvalidOperationException($"You are not allowed to set properties for both single-endpoint and multiple-endpoint. Please unset {nameof(ServiceEndpoint)} or {nameof(ServiceEndpoints)}");
            }
        }

        private void ValidateServiceTransportType()
        {
            if (!Enum.IsDefined(typeof(ServiceTransportType), ServiceTransportType))
            {
                throw new ArgumentOutOfRangeException($"Not supported service transport type. " +
                    $"Supported transports type are {ServiceTransportType.Transient} and {ServiceTransportType.Persistent}");
            }
        }
    }
}