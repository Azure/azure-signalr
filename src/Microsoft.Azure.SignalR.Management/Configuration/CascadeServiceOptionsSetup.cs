// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Management.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class CascadeServiceOptionsSetup : CascadeOptionsSetup<ServiceOptions, ServiceManagerOptions>
    {
        public CascadeServiceOptionsSetup(IOptionsMonitor<ServiceManagerOptions> sourceMonitor) : base(sourceMonitor)
        {
        }

        protected override void Convert(ServiceOptions target, ServiceManagerOptions source)
        {
            target.ConnectionString = source.ConnectionString;
            target.ApplicationName = source.ApplicationName;
            target.Endpoints = source.ServiceEndpoints;
            target.Proxy = source.Proxy;
            target.InitialHubServerConnectionCount = source.ConnectionCount;
        }
    }
}