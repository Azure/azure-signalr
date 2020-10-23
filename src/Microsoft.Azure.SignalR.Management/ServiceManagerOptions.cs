﻿// Copyright (c) Microsoft. All rights reserved.
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
        /// Gets or sets a service endpoint of Azure SignalR Service instance by connection string.
        /// </summary>
        public string ConnectionString { get; set; } = null;

        /// <summary>
        /// Gets or sets the proxy used when ServiceManager will attempt to connect to Azure SignalR Service.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets a service endpoint of Azure SignalR Service.
        /// </summary>
        public ServiceEndpoint ServiceEndpoint { get; set; }

        /// <summary>
        /// <para>Gets service endpoint(s) of Azure SignalR Service set by any one of the properties 'ConnectionString', 'ServiceEndpoint' or 'ServiceEndpoints'.</para>
        /// <para>Sets multiple service endpoints of Azure SignalR Service.</para>
        /// </summary>
        internal ServiceEndpoint[] ServiceEndpoints
        {
            get
            {
                if (_serviceEndpoints != null)
                {
                    return _serviceEndpoints;
                }
                if (ServiceEndpoint != null)
                {
                    return new ServiceEndpoint[] { ServiceEndpoint };
                }
                else
                {
                    return new ServiceEndpoint[] { new ServiceEndpoint(ConnectionString) };
                }
            }
            set
            {
                if (value != null && value.Length == 0)
                {
                    throw new ArgumentException("The length of array is zero.", nameof(ServiceEndpoints));
                }
                _serviceEndpoints = value;
            }
        } //not ready for public use

        /// <summary>
        /// Gets or sets the transport type to Azure SignalR Service. Default value is Transient.
        /// </summary>
        public ServiceTransportType ServiceTransportType { get; set; } = ServiceTransportType.Transient;

        private ServiceEndpoint[] _serviceEndpoints;

        /// <summary>
        /// A method would be called management SDK to validate options.
        /// </summary>
        public void ValidateOptions()
        {
            ValidateServiceEndpoint();
            ValidateServiceTransportType();
        }

        private void ValidateServiceEndpoint()
        {
            var notNullCount = 0;
            if (ConnectionString != null)
            {
                notNullCount += 1;
            }
            if (ServiceEndpoint != null)
            {
                notNullCount += 1;
            }
            if (ServiceEndpoints != null)
            {
                notNullCount += 1;
            }

            if (notNullCount == 0)
            {
                throw new InvalidOperationException($"Service endpoint(s) is/are not configured. Please set one of the following properties {nameof(ConnectionString)}, {nameof(ServiceEndpoint)}, {nameof(ServiceEndpoints)}.");
            }
            if (notNullCount > 1)
            {
                throw new InvalidOperationException($"Please set ONLY one of the following properties: {nameof(ConnectionString)}, {nameof(ServiceEndpoint)}, {nameof(ServiceEndpoints)}.");
            }
        }

        private void ValidateServiceTransportType()
        {
            if (!Enum.IsDefined(typeof(ServiceTransportType), ServiceTransportType))
            {
                throw new ArgumentOutOfRangeException($"Not supported service transport type. " +
                    $"Supported transport types are {ServiceTransportType.Transient} and {ServiceTransportType.Persistent}.");
            }
        }
    }
}
