// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Configurable options for Azure SignalR Management SDK.
    /// </summary>
    public class ServiceManagerOptions
    {
        /// <summary>
        /// Gets or sets the transport type to Azure SignalR Service. Default value is Transient.
        /// </summary>
        public ServiceTransportType ServiceTransportType { get; set; } = ServiceTransportType.Transient;

        /// <summary>
        /// Gets or sets the connection string of Azure SignalR Service instance.
        /// </summary>
        public string ConnectionString { get; set; } = null;

        internal void ValidateOptions()
        {
            ValidateConnectionString();
            ValidateServiceTransportType();
        }

        private void ValidateConnectionString()
        {
            // if the connection string is invalid, exceptions will be thrown.
            ConnectionStringParser.Parse(ConnectionString);
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