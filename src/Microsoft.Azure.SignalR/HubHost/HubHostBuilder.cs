// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR
{
    public class HubHostBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly EndpointProvider _endpointProvider;
        private readonly TokenProvider _tokenProvider;

        public HubHostBuilder(IServiceProvider serviceProvider, EndpointProvider endpointProvider,
            TokenProvider tokenProvider)
        {
            _serviceProvider = serviceProvider;
            _endpointProvider = endpointProvider;
            _tokenProvider = tokenProvider;
        }

        public HubHost<THub> UseHub<THub>() where THub: Hub
        {
            var hubHost = _serviceProvider.GetRequiredService<HubHost<THub>>();
            hubHost.Configure(_endpointProvider, _tokenProvider);
            var connectionBuilder = new ConnectionBuilder(_serviceProvider);
            connectionBuilder.UseHub<THub>();
            var app = connectionBuilder.Build();
            hubHost.StartAsync(app).GetAwaiter().GetResult();
            return hubHost;
        }
    }
}
