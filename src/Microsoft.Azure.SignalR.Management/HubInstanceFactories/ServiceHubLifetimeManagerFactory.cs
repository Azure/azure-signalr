// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
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

        public IServiceHubLifetimeManager Create(string hubName)
        {
            switch (_options.ServiceTransportType)
            {
                case ServiceTransportType.Persistent:
                    {
                        var container = _serviceProvider.GetRequiredService<IServiceConnectionContainer>();
                        var connectionManager = new ServiceConnectionManager<Hub>();
                        connectionManager.SetServiceConnection(container);
                        return ActivatorUtilities.CreateInstance<WebSocketsHubLifetimeManager<Hub>>(_serviceProvider, connectionManager);
                    }
                case ServiceTransportType.Transient:
                    {
                        var restHubProtocol = _serviceProvider.GetService<IRestHubProtocol>();
                        var payloadSerializerSettings = _options.JsonSerializerSettings;
                        //Currently RestHubProtocol only has Newtonsoft
                        if (restHubProtocol != null)
                        {
                            var newtonsoftServiceHubProtocolOptions = _serviceProvider.GetService<IOptions<NewtonsoftServiceHubProtocolOptions>>();
                            payloadSerializerSettings = newtonsoftServiceHubProtocolOptions.Value.PayloadSerializerSettings;
                        }
                        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
                        var restClient = new RestClient(httpClientFactory, payloadSerializerSettings);
                        return new RestHubLifetimeManager(hubName, new ServiceEndpoint(_options.ConnectionString), _options.ProductInfo, _options.ApplicationName, restClient);
                    }
                default: throw new InvalidEnumArgumentException(nameof(ServiceManagerOptions.ServiceTransportType), (int)_options.ServiceTransportType, typeof(ServiceTransportType));
            }
        }
    }
}