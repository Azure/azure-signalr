﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

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
        /// Gets or sets multiple service endpoints of Azure SignalR Service instances.
        /// </summary>
        public ServiceEndpoint[] ServiceEndpoints { get; set; }

        /// <summary>
        /// Gets or sets the proxy used when ServiceManager will attempt to connect to Azure SignalR Service.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        /// <summary>
        /// Gets or sets the transport type to Azure SignalR Service. Default value is Transient.
        /// </summary>
        public ServiceTransportType ServiceTransportType { get; set; } = ServiceTransportType.Transient;

        /// <summary>
        /// Gets the json serializer settings that will be used to serialize content sent to Azure SignalR Service.
        /// </summary>
        [Obsolete("Use ServiceManagerBuilder.WithNewtonsoftJson instead.")]
        public JsonSerializerSettings JsonSerializerSettings { get; } = new JsonSerializerSettings();

        /// <summary>
        /// Gets or sets a value indicating whether message tracing ID is append to messages.
        /// </summary>
        // not ready
        internal bool EnableMessageTracing { get; set; } = false;

        internal string ProductInfo { get; set; }

        internal void ValidateOptions()
        {
            if ((ServiceEndpoints == null || ServiceEndpoints.Length == 0) && string.IsNullOrWhiteSpace(ConnectionString))
            {
                throw new InvalidOperationException($"{nameof(ServiceEndpoints)} is empty. {nameof(ConnectionString)} is  null, empty, or consists only of white-space.");
            }
            // forbid multiple endpoints in transient mode.
            if (ServiceTransportType == ServiceTransportType.Transient )
            {
                var count = ConnectionString == null ? 0 : 1;
                if (ServiceEndpoints != null)
                {
                    count += ServiceEndpoints.Length;
                }
                if (count > 1)
                {
                    throw new InvalidOperationException($"Multiple service endpoints are set via {ConnectionString} or {ServiceEndpoints}, but transient transport type does not support multiple service endpoints yet.");
                }
            }
        }
    }
}