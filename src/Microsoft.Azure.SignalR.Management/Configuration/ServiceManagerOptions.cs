// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Configurable options for Azure SignalR Management SDK.
    /// </summary>
    public class ServiceManagerOptions
    {
        /// <summary>
        /// The section name used to get <see cref="ServiceManagerOptions"/> value from <see cref="IConfiguration.GetSection(string)"/>
        /// </summary>
        public const string Section = "Azure:SignalR";

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
        internal ServiceEndpoint ServiceEndpoint { get; set; }

        /// <summary>
        /// Sets multiple service endpoints of Azure SignalR Service.
        /// </summary>
        internal ServiceEndpoint[] ServiceEndpoints { get; set; } //not ready for public use

        /// <summary>
        /// Gets or sets the transport type to Azure SignalR Service. Default value is Transient.
        /// </summary>
        public ServiceTransportType ServiceTransportType { get; set; } = ServiceTransportType.Transient;

        /// <summary>
        /// Method called by management SDK to validate options.
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
            if (ServiceEndpoints != null && ServiceEndpoints.Length == 0)
            {
                throw new InvalidOperationException($"The length of parameter {nameof(ServiceEndpoints)} is zero.");
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