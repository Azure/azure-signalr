// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Common.RestClients;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManager : IServiceManager
    {
        private readonly ServiceManagerOptions _serviceManagerOptions;
        private readonly ServiceEndpointProvider _endpointProvider;
        private readonly IServerNameProvider _serverNameProvider;
        private readonly ServiceEndpoint _endpoint;
        private readonly string _productInfo;
        private readonly RestClientFactory _restClientFactory;

        internal ServiceManager(ServiceManagerOptions serviceManagerOptions, RestClientFactory restClientFactory)
        {
            _serviceManagerOptions = serviceManagerOptions;

            _endpoint = serviceManagerOptions.UnifiedEndpoints.Single();//temp solution

            _serverNameProvider = new DefaultServerNameProvider();

            var serviceOptions = Options.Create(new ServiceOptions
            {
                ApplicationName = _serviceManagerOptions.ApplicationName,
                Proxy = serviceManagerOptions.Proxy
            }).Value;

            _endpointProvider = new ServiceEndpointProvider(_serverNameProvider, _endpoint, serviceOptions);

            _productInfo = serviceManagerOptions.ProductInfo;
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
                        var serviceProtocol = new ServiceProtocol();
                        var clientConnectionManager = new ClientConnectionManager();
                        var clientConnectionFactory = new ClientConnectionFactory();
                        ConnectionDelegate connectionDelegate = connectionContext => Task.CompletedTask;
                        var serviceConnectionFactory = new ServiceConnectionFactory(
                            serviceProtocol,
                            clientConnectionManager,
                            connectionFactory,
                            loggerFactory,
                            connectionDelegate,
                            clientConnectionFactory,
                            new DefaultServerNameProvider()
                            );
                        var weakConnectionContainer = new WeakServiceConnectionContainer(
                            serviceConnectionFactory,
                            _serviceManagerOptions.ConnectionCount,
                            new HubServiceEndpoint(hubName, _endpointProvider, _endpoint),
                            loggerFactory?.CreateLogger(nameof(WeakServiceConnectionContainer)) ?? NullLogger.Instance);

                        var serviceCollection = new ServiceCollection();
                        serviceCollection.AddSignalRCore();
                        serviceCollection.AddSingleton<IConfigureOptions<HubOptions>, ManagementHubOptionsSetup>();

                        if (loggerFactory != null)
                        {
                            serviceCollection.AddSingleton(typeof(ILoggerFactory), loggerFactory);
                        }

                        serviceCollection
                            .AddLogging()
                            .AddSingleton(typeof(IConnectionFactory), sp => connectionFactory)
                            .AddSingleton(typeof(HubLifetimeManager<>), typeof(WebSocketsHubLifetimeManager<>))
                            .AddSingleton(typeof(IServiceConnectionManager<>), typeof(ServiceConnectionManager<>))
                            .AddSingleton(typeof(IServiceConnectionContainer), sp => weakConnectionContainer);

                        var success = false;
                        ServiceProvider serviceProvider = null;
                        try
                        {
                            serviceProvider = serviceCollection.BuildServiceProvider();

                            var serviceConnectionManager = serviceProvider.GetRequiredService<IServiceConnectionManager<Hub>>();
                            serviceConnectionManager.SetServiceConnection(weakConnectionContainer);
                            _ = serviceConnectionManager.StartAsync();

                            // wait until service connection established
                            await weakConnectionContainer.ConnectionInitializedTask.OrTimeout(cancellationToken);

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
                        var restHubLifetimeManager = new RestHubLifetimeManager(_endpoint, hubName, _productInfo, _serviceManagerOptions.ApplicationName);
                        serviceCollection.AddSingleton(typeof(HubLifetimeManager<Hub>), sp => restHubLifetimeManager);

                        var serviceProvider = serviceCollection.BuildServiceProvider();
                        var hubContext = serviceProvider.GetRequiredService<IHubContext<Hub>>();
                        return new ServiceHubContext(hubContext, restHubLifetimeManager, serviceProvider);
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
            return _endpointProvider.GenerateClientAccessTokenAsync(hubName, claimsWithUserId, lifeTime).Result;
        }

        public string GetClientEndpoint(string hubName)
        {
            return _endpointProvider.GetClientEndpoint(hubName, null, null);
        }

        public async Task<bool> IsServiceHealthy(CancellationToken cancellationToken)
        {
            using var restClient = _restClientFactory.Create(_endpoint);
            try
            {
                var healthApi = restClient.HealthApi;
                using var response = await healthApi.GetHealthStatusWithHttpMessagesAsync(cancellationToken: cancellationToken);
                return true;
            }
            catch (HttpOperationException e) when ((int)e.Response.StatusCode >= 500 && (int)e.Response.StatusCode < 600)
            {
                return false;
            }
            catch (Exception ex)
            {
                throw ex.WrapAsAzureSignalRException(restClient.BaseUri);
            }
        }
    }
}