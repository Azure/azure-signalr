// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Management.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceOptionsSetup : CascadeOptionsSetup<ServiceOptions>
    {
        public ServiceOptionsSetup(IOptions<ServiceManagerOptions> initialSource, IConfiguration configuration = null) : base(initialSource, configuration)
        {
        }

        protected override void Convert(ServiceOptions target, ServiceManagerOptions source)
        {
            target.ApplicationName = source.ApplicationName;
            target.Endpoints = source.ServiceEndpoints ?? (source.ServiceEndpoint != null
                    ? (new ServiceEndpoint[] { source.ServiceEndpoint })
                    : (new ServiceEndpoint[] { new ServiceEndpoint(source.ConnectionString) }));
            target.Proxy = source.Proxy;
            target.ConnectionCount = source.ConnectionCount;
        }
    }
}