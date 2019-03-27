// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Startup
{
    internal class AzureSignalRHostedService
    {
#if NETCOREAPP3_0
        private readonly EndpointDataSource _dataSource;
        private readonly IServiceProvider _serviceProvider;

        public AzureSignalRHostedService(EndpointDataSource dataSource, IServiceProvider serviceProvider)
        {
            _dataSource = dataSource;
            _serviceProvider = serviceProvider;
        }

        public void Start()
        {
            var dispatcher = new ServiceHubDispatcher(_serviceProvider);

            var hubTypes = _dataSource.Endpoints.Select(e => e.Metadata.GetMetadata<HubMetadata>()?.HubType)
                                   .Where(hubType => hubType != null)
                                   .Distinct()
                                   .ToList();

            foreach (var hubType in hubTypes)
            {
                var app = new ConnectionBuilder(_serviceProvider)
                            .UseHub(hubType)
                            .Build();

                dispatcher.Start(hubType, app);
            }
        }
#endif
    }
}
