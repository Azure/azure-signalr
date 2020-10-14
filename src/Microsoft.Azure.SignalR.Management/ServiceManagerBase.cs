// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal abstract class ServiceManagerBase : IServiceManager
    {
        protected readonly ServiceManagerOptions _serviceManagerOptions;
        protected readonly IServerNameProvider _serverNameProvider;
        protected readonly string _productInfo;
        protected readonly RestClientFactory _restClientFactory;

        internal ServiceManagerBase(ServiceManagerOptions serviceManagerOptions, string productInfo, RestClientFactory restClientFactory)
        {
            _serviceManagerOptions = serviceManagerOptions;

            _serverNameProvider = new DefaultServerNameProvider();

            _productInfo = productInfo;
            _restClientFactory = restClientFactory;
        }

        public async Task<IServiceHubContext> CreateHubContextAsync(string hubName, ILoggerFactory loggerFactory = null, CancellationToken cancellationToken = default)
        {
            loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            switch (_serviceManagerOptions.ServiceTransportType)
            {
                case ServiceTransportType.Persistent:
                    {
                        var connectionFactory = new ManagementConnectionFactory(_productInfo, new ConnectionFactory(_serverNameProvider, loggerFactory));
                        var serviceConnectionFactory = new ServiceConnectionFactory(
                            new ServiceProtocol(),
                            new ClientConnectionManager(),
                            connectionFactory,
                            loggerFactory,
                            connectionContext => Task.CompletedTask,
                            new ClientConnectionFactory(),
                            new DefaultServerNameProvider()
                            );

                        var serviceCollection = new ServiceCollection();
                        serviceCollection.AddSignalRCore();
                        serviceCollection.AddSingleton<IConfigureOptions<HubOptions>, ManagementHubOptionsSetup>();
                        serviceCollection.AddSingleton(typeof(ILoggerFactory), loggerFactory);

                        serviceCollection
                            .AddLogging()
                            .AddSingleton(typeof(IServiceConnectionFactory), serviceConnectionFactory)
                            .AddSingleton(typeof(IConnectionFactory), sp => connectionFactory)
                            .AddSingleton(typeof(HubLifetimeManager<>), typeof(WebSocketsHubLifetimeManager<>))
                            .AddSingleton(typeof(IServiceConnectionManager<>), typeof(ServiceConnectionManager<>));

                        ConfigurePersistentServiceCollection(serviceCollection);

                        var success = false;
                        ServiceProvider serviceProvider = null;
                        try
                        {
                            serviceProvider = serviceCollection.BuildServiceProvider();

                            var connectionContainer = GetPersistentConnectionContainer(hubName, serviceProvider);

                            var serviceConnectionManager = serviceProvider.GetRequiredService<IServiceConnectionManager<Hub>>();
                            serviceConnectionManager.SetServiceConnection(connectionContainer);
                            _ = serviceConnectionManager.StartAsync();

                            // wait until service connection established
                            await connectionContainer.ConnectionInitializedTask.OrTimeout(cancellationToken);

                            var webSocketsHubLifetimeManager = (WebSocketsHubLifetimeManager<Hub>)serviceProvider.GetRequiredService<HubLifetimeManager<Hub>>();

                            var hubContext = serviceProvider.GetRequiredService<IHubContext<Hub>>();
                            var serviceHubContext = new ServiceHubContext(hubContext, webSocketsHubLifetimeManager, serviceProvider);
                            success = true;
                            return serviceHubContext;
                        }
                        finally
                        {
                            if (!success)
                            {
                                serviceProvider?.Dispose();
                            }
                        }
                    }
                case ServiceTransportType.Transient:
                    {
                        var serviceCollection = new ServiceCollection();
                        serviceCollection.AddSignalRCore();

                        // remove default hub lifetime manager
                        var serviceDescriptor = serviceCollection.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(HubLifetimeManager<>));
                        serviceCollection.Remove(serviceDescriptor);

                        // add rest hub lifetime manager
                        var restHubLifetimeManager = GetTransientHubLifetimeManager(hubName);
                        serviceCollection.AddSingleton(typeof(HubLifetimeManager<Hub>), sp => restHubLifetimeManager);

                        var serviceProvider = serviceCollection.BuildServiceProvider();
                        var hubContext = serviceProvider.GetRequiredService<IHubContext<Hub>>();
                        return new ServiceHubContext(hubContext, (IHubLifetimeManagerForUserGroup)restHubLifetimeManager, serviceProvider);
                    }
                default:
                    throw new ArgumentException("Not supported service transport type.");
            }
        }

        public abstract string GenerateClientAccessToken(string hubName, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null);
        public abstract string GenerateClientAccessToken(string hubName, ServiceEndpoint endpoint, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null);
        public abstract string GetClientEndpoint(string hubName);
        public abstract ServiceEndpoint GetClientEndpoint(HttpContext httpContext);
        public abstract string GetClientEndpoint(string hubName, ServiceEndpoint endpoint);
        public abstract Task<bool> IsServiceHealthy(CancellationToken cancellationToken);

        protected abstract void ConfigurePersistentServiceCollection(ServiceCollection services);

        protected abstract IServiceConnectionContainer GetPersistentConnectionContainer(string hubName, ServiceProvider serviceProvider);

        protected abstract HubLifetimeManager<Hub> GetTransientHubLifetimeManager(string hubName);

    }
}