// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Management.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceOptionsSetup : CascadeOptionsSetup<ServiceOptions, ServiceManagerContext>
    {
        public ServiceOptionsSetup(IOptionsMonitor<ServiceManagerContext> sourceMonitor) : base(sourceMonitor)
        {
        }

        protected override void Convert(ServiceOptions target, ServiceManagerContext source)
        {
            target.ApplicationName = source.ApplicationName;
            target.Endpoints = source.ServiceEndpoints;
            target.Proxy = source.Proxy;
            target.ConnectionCount = source.ConnectionCount;
        }
    }
}