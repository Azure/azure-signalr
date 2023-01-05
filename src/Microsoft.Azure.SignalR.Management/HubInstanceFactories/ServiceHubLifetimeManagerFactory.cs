// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using Azure.Core.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
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
                        CheckHubProtocols();
#pragma warning disable CS0618 // Type or member is obsolete
                        var payloadSerializerSettings = _options.JsonSerializerSettings;
#pragma warning restore CS0618 // Type or member is obsolete
                        //Currently RestHubProtocol only has Newtonsoft
                        var objectSerializer = _options.ObjectSerializer;
                        if (objectSerializer == null)
                        {
                            // keep backward compatibility
                            objectSerializer = new NewtonsoftJsonObjectSerializer(payloadSerializerSettings);
                        }
                        var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
                        var serviceEndpoint = _serviceProvider.GetRequiredService<IServiceEndpointManager>().Endpoints.First().Key;
                        var restClient = new RestClient(httpClientFactory, objectSerializer, _options.EnableMessageTracing);
                        return new RestHubLifetimeManager<THub>(hubName, serviceEndpoint, _options.ProductInfo, _options.ApplicationName, restClient);
                    }
                default: throw new InvalidEnumArgumentException(nameof(ServiceManagerOptions.ServiceTransportType), (int)_options.ServiceTransportType, typeof(ServiceTransportType));
            }
        }

        private void CheckHubProtocols()
        {
            var protocols = _serviceProvider.GetServices<IHubProtocol>().ToArray();
            if (protocols.Length > 1 || protocols.Where(p => p.Name.Equals(Constants.Protocol.MessagePack)).Any())
            {
                throw new InvalidOperationException("ServiceManagerBuilder.WithHubProtocols method is not supported for transient(default) mode yet.");
            }
        }
    }
}