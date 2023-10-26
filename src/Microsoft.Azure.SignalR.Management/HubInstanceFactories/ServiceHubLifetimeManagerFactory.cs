// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubLifetimeManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceManagerOptions _options;

        public ServiceHubLifetimeManagerFactory(IServiceProvider sp, IOptions<ServiceManagerOptions> context)
        {
            _serviceProvider = sp;
            _options = context.Value;
        }

        public IServiceHubLifetimeManager<THub> Create<THub>(string hubName) where THub : Hub
        {
            switch (_options.ServiceTransportType)
            {
                case ServiceTransportType.Persistent:
                    {
                        var container = _serviceProvider.GetRequiredService<IServiceConnectionContainer>();
                        var connectionManager = new ServiceConnectionManager<THub>();
                        connectionManager.SetServiceConnection(container);
                        return ActivatorUtilities.CreateInstance<WebSocketsHubLifetimeManager<THub>>(_serviceProvider, connectionManager);
                    }
                case ServiceTransportType.Transient:
                    {
                        var payloadBuilderResolver = _serviceProvider.GetRequiredService<PayloadBuilderResolver>();
                        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
                        var serviceEndpoint = _serviceProvider.GetRequiredService<IServiceEndpointManager>().Endpoints.First().Key;
                        var restClient = new RestClient(httpClientFactory, payloadBuilderResolver.GetPayloadContentBuilder());
                        return new RestHubLifetimeManager<THub>(hubName, serviceEndpoint, _options.ApplicationName, restClient);
                    }
                default: throw new InvalidEnumArgumentException(nameof(ServiceManagerOptions.ServiceTransportType), (int)_options.ServiceTransportType, typeof(ServiceTransportType));
            }
        }
    }
}