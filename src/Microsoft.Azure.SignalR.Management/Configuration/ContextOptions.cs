// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;

namespace Microsoft.Azure.SignalR.Management
{
    //todo public later
    internal class ContextOptions
    {
        //Users not allowed to configure it
        internal string ProductInfo { get; set; }

        /// <summary>
        /// Gets or sets the service endpoints.
        /// </summary>
        public ServiceEndpoint[] ServiceEndpoints { get; set; }

        /// <summary>
        /// Gets or sets the ApplicationName which will be prefixed to each hub name
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Gets or sets the total number of connections from SDK to Azure SignalR Service. Default value is 1.
        /// </summary>
        public int ConnectionCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets the proxy used when ServiceManager will attempt to connect to Azure SignalR Service.
        /// </summary>
        public IWebProxy Proxy { get; set; }
        internal ServiceTransportType ServiceTransportType { get; set; } = ServiceTransportType.Persistent;

        internal void ValidateOptions()
        {
            if (ServiceEndpoints.Length == 0)
            {
                throw new InvalidOperationException($"Service endpoint(s) is/are not configured.");
            }
        }
    }
}