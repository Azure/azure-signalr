// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;

namespace Microsoft.Azure.SignalR.Management
{
    //todo public later
    internal class ContextOptions
    {
        /// <summary>
        /// Gets or sets the service endpoints.
        /// </summary>
        public ServiceEndpoint[] ServiceEndpoints { get; set; }

        /// <summary>
        /// Gets or sets the application name, it can be useful when you'd like to share the same Azure SignalR service for different applications with the same hub name.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Gets or sets the proxy used when SDK attempts to connect to Azure SignalR Service.
        /// </summary>
        public IWebProxy Proxy { get; set; }

        internal int ConnectionCount { get; set; } = 3;
        internal ServiceTransportType ServiceTransportType { get; set; } = ServiceTransportType.Persistent;
        internal string ProductInfo { get; set; }

        internal void ValidateOptions()
        {
            if (ServiceEndpoints.Length == 0)
            {
                throw new InvalidOperationException($"Service endpoint(s) is/are not configured.");
            }
        }
    }
}