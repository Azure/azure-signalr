// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !NETSTANDARD2_0
using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class AzureSignalRHostedService
    {
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

            foreach (var endpoint in _dataSource.Endpoints)
            {
                var hubMetadata = endpoint.Metadata.GetMetadata<HubMetadata>();
                var negotiateMetadata = endpoint.Metadata.GetMetadata<NegotiateMetadata>();

                // Skip if not a hub or is negotiate endpoint
                if (hubMetadata == null || negotiateMetadata != null)
                {
                    continue;
                }

                // Start the application for each of the hub types
                var app = new ConnectionBuilder(_serviceProvider)
                    .UseHub(hubMetadata.HubType)
                    .Build();

                // Flow the endpoint to the dispatcher so it can be set on the HttpContextFeature.
                dispatcher.Start(endpoint, hubMetadata.HubType, app);
            }
        }
    }
}
#endif