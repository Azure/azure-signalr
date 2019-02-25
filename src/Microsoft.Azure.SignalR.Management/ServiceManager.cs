// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.Azure.SignalR.Common.ServiceConnections;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManager : IServiceManager
    {
        private readonly ServiceManagerOptions _serviceManagerOptions;
        private readonly ServiceEndpointProvider _endpoint;
        private const int ServerConnectionCount = 1;

        internal ServiceManager(ServiceManagerOptions serviceManagerOptions)
        {
            _serviceManagerOptions = serviceManagerOptions;
            _endpoint = new ServiceEndpointProvider(new ServiceEndpoint(_serviceManagerOptions.ConnectionString));
        }

        public Task<IServiceHubContext> CreateHubContextAsync(string hubName)
        {
            switch (_serviceManagerOptions.ServiceTransportType)
            {
                case ServiceTransportType.Persistent:
                    {
                        var loggerFactory = new LoggerFactory();
                        var endpoint = new ServiceEndpoint(_serviceManagerOptions.ConnectionString);
                        var serverOptions = new ServiceOptions
                        {
                            ConnectionString = _serviceManagerOptions.ConnectionString,
                            ConnectionCount = ServerConnectionCount,
                            Endpoints = new ServiceEndpoint[] { endpoint }
                        };
                        var options = Options.Create(serverOptions);
                        var endpointManager = new ServiceEndpointManager(options, loggerFactory);
                        var provider = endpointManager.GetEndpointProvider(endpoint);
                        var connectionFactory = new ConnectionFactory(hubName, provider, loggerFactory);
                        var serviceProtocol = new ServiceProtocol();
                        var clientConnectionManager = new ClientConnectionManager();
                        var clientConnectionFactory = new ClientConnectionFactory();
                        ConnectionDelegate connectionDelegate = connectionContext => Task.CompletedTask;
                        var serviceConnectionFactory = new ServiceConnectionFactory(serviceProtocol, clientConnectionManager, loggerFactory, connectionDelegate, clientConnectionFactory);
                        var weakConnectionContainer = new WeakServiceConnectionContainer(serviceConnectionFactory, connectionFactory, ServerConnectionCount, endpoint);

                        var serviceCollection = new ServiceCollection();
                        serviceCollection.AddSignalRCore();

                        serviceCollection
                            .AddSingleton(typeof(ILogger<DefaultHubProtocolResolver>), NullLogger<DefaultHubProtocolResolver>.Instance)
                            .AddSingleton(typeof(HubLifetimeManager<>), typeof(ServiceLifetimeManagerCore<>))
                            .AddSingleton(typeof(IServiceEndpointManager), typeof(ServiceEndpointManager))
                            .AddSingleton(typeof(IServiceProtocol), typeof(ServiceProtocol))
                            .AddSingleton(typeof(IServiceConnectionManager<>), typeof(ServiceConnectionManager<>))
                            .AddSingleton<IHostedService, HeartBeat>()
                            .AddSingleton<NegotiateHandler>()
                            .AddSingleton(typeof(IServiceConnectionContainer), weakConnectionContainer);
                            
                        var services = serviceCollection.BuildServiceProvider();

                        var serviceConnectionManager = services.GetRequiredService<IServiceConnectionManager<Hub>>();
                        serviceConnectionManager.SetServiceConnection(weakConnectionContainer);
                        _ = serviceConnectionManager.StartAsync();

                        var protocolResolver = services.GetRequiredService<IHubProtocolResolver>();
                        var websocketsHubLifetimeManager = new WebsocketsHubLifetimeManager(serviceConnectionManager, protocolResolver);

                        var hubContext = services.GetRequiredService<IHubContext<Hub>>();
                        var serviceHubContext = new ServiceHubContext(hubContext, websocketsHubLifetimeManager);
                        return Task.FromResult<IServiceHubContext>(serviceHubContext);
                    }
                case ServiceTransportType.Transient:
                    {
                        var serviceCollection = new ServiceCollection();
                        serviceCollection.AddSignalRCore();

                        // remove default hub lifetime manager
                        var serviceDescriptor = serviceCollection.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(HubLifetimeManager<>));
                        serviceCollection.Remove(serviceDescriptor);

                        // add rest hub lifetime manager
                        var restHubLifetimeManager = new RestHubLifetimeManager(_serviceManagerOptions, hubName);
                        serviceCollection.AddSingleton(typeof(HubLifetimeManager<Hub>), restHubLifetimeManager);

                        var services = serviceCollection.BuildServiceProvider();
                        var hubContext = services.GetRequiredService<IHubContext<Hub>>();
                        var serviceHubContext = new ServiceHubContext(hubContext, restHubLifetimeManager);
                        return Task.FromResult<IServiceHubContext>(serviceHubContext);
                    }
                default:
                    throw new ArgumentException("Not supported service transport type.");
            }
        }

        public string GenerateClientAccessToken(string hubName, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null)
        {
            var claimsWithUserId = new List<Claim>();
            if (userId != null)
            {
                claimsWithUserId.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            };
            if (claims != null)
            {
                claimsWithUserId.AddRange(claims);
            }
            return _endpoint.GenerateClientAccessToken(hubName, claimsWithUserId, lifeTime);
        }

        public string GetClientEndpoint(string hubName) => _endpoint.GetClientEndpoint(hubName, null, null);
    }
}