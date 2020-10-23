// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManagerContext
    {
        public string ProductInfo { get; set; }

        public ServiceEndpoint[] ServiceEndpoints { get; set; }

        public string ApplicationName { get; set; }

        public int ConnectionCount { get; set; }

        public IWebProxy Proxy { get; set; }

        public ServiceTransportType ServiceTransportType { get; set; } = ServiceTransportType.Transient;

        public void SetValueFromOptions(ServiceManagerOptions options)
        {
            ServiceEndpoints = options.ServiceEndpoints ?? (options.ServiceEndpoint != null
                    ? (new ServiceEndpoint[] { options.ServiceEndpoint })
                    : (new ServiceEndpoint[] { new ServiceEndpoint(options.ConnectionString) }));
            ApplicationName = options.ApplicationName;
            ConnectionCount = options.ConnectionCount;
            Proxy = options.Proxy;
            ServiceTransportType = options.ServiceTransportType;
        }
    }
}
