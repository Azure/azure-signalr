// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Management.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceOptionsSetup : CascadeOptionsSetup<ServiceManagerContext, ServiceOptions>
    {
        public ServiceOptionsSetup(IOptionsMonitor<ServiceManagerContext> monitor, IOptionsChangeTokenSource<ServiceManagerContext> changeTokenSource = null) : base(monitor, changeTokenSource)
        {
        }

        public override void Configure(ServiceOptions options)
        {
            var context = _monitor.CurrentValue;
            options.ApplicationName = context.ApplicationName;
            options.Endpoints = context.ServiceEndpoints;
            options.Proxy = context.Proxy;
            options.ConnectionCount = context.ConnectionCount;
        }
    }
}