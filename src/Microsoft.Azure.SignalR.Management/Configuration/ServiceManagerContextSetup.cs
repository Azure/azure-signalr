// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Management.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManagerContextSetup : CascadeOptionsSetup<ServiceManagerContext, ServiceManagerOptions>
    {
        public ServiceManagerContextSetup(IOptionsMonitor<ServiceManagerOptions> sourceMonitor) : base(sourceMonitor)
        {
        }

        protected override void Convert(ServiceManagerContext target, ServiceManagerOptions source)
        {
            target.ServiceEndpoints = source.ServiceEndpoints ?? (source.ServiceEndpoint != null
                    ? (new ServiceEndpoint[] { source.ServiceEndpoint })
                    : (new ServiceEndpoint[] { new ServiceEndpoint(source.ConnectionString) }));
            target.ApplicationName = source.ApplicationName;
            target.ConnectionCount = source.ConnectionCount;
            target.Proxy = source.Proxy;
            target.ServiceTransportType = source.ServiceTransportType;
        }
    }
}