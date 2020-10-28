// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Management.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManagerContextSetup : CascadeOptionsSetup<ServiceManagerOptions, ServiceManagerContext>
    {
        public ServiceManagerContextSetup(IOptionsMonitor<ServiceManagerOptions> monitor, IOptionsChangeTokenSource<ServiceManagerOptions> changeTokenSource = null) : base(monitor, changeTokenSource)
        {
        }

        public override void Configure(ServiceManagerContext options)
        {
            var serviceManagerOptions = _monitor.CurrentValue;
            options.ServiceEndpoints = serviceManagerOptions.ServiceEndpoints ?? (serviceManagerOptions.ServiceEndpoint != null
                    ? (new ServiceEndpoint[] { serviceManagerOptions.ServiceEndpoint })
                    : (new ServiceEndpoint[] { new ServiceEndpoint(serviceManagerOptions.ConnectionString) }));
            options.ApplicationName = serviceManagerOptions.ApplicationName;
            options.ConnectionCount = serviceManagerOptions.ConnectionCount;
            options.Proxy = serviceManagerOptions.Proxy;
            options.ServiceTransportType = serviceManagerOptions.ServiceTransportType;
        }
    }
}