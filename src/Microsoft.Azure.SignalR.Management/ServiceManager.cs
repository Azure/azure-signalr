﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common.RestClients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR.Management
{
    //todo public
    internal class ServiceManager : IServiceManager
    {
        private readonly RestClientFactory _restClientFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly IReadOnlyCollection<ServiceDescriptor> _services;
        private readonly ServiceEndpoint _endpoint;
        private readonly IServiceEndpointProvider _endpointProvider;

        public ServiceManager(IReadOnlyCollection<ServiceDescriptor> services, IServiceProvider serviceProvider, RestClientFactory restClientFactory, IServiceEndpointManager endpointManager)
        {
            _services = services;
            _serviceProvider = serviceProvider;
            _restClientFactory = restClientFactory;
            _endpoint = endpointManager.Endpoints.Keys.First();
            _endpointProvider = endpointManager.GetEndpointProvider(_endpoint);
        }


        //todo obsolete
        async Task<IServiceHubContext> IServiceManager.CreateHubContextAsync(string hubName, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
        {
            var builder = new ServiceHubContextBuilder(new ServiceCollection().Add(_services));
            if (loggerFactory != null)
            {
                builder.WithLoggerFactory(loggerFactory);
            }
            var serviceHubContext = await builder.CreateAsync(hubName, cancellationToken);
            return serviceHubContext;
        }

        public async Task<ServiceHubContext> CreateHubContextAsync(string hubName, CancellationToken cancellationToken)
        {
            var services = new ServiceCollection().Add(_services);
            using var serviceProvider = services.BuildServiceProvider();
            var transportType = serviceProvider.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.ServiceTransportType;
            services.AddSingleton(_services);
            services.AddHub(hubName, transportType);
            ServiceHubContextImpl serviceHubContext = null;
            try
            {
                //build
                serviceHubContext = services.BuildServiceProvider()
                    .GetRequiredService<ServiceHubContextImpl>();
                //initialize
                var connectionContainer = serviceHubContext.ServiceProvider.GetService<IServiceConnectionContainer>();
                if (connectionContainer != null)
                {
                    await connectionContainer.ConnectionInitializedTask.OrTimeout(cancellationToken);
                }
                return serviceHubContext.ServiceProvider.GetRequiredService<ServiceHubContextImpl>();
            }
            catch
            {
                if (serviceHubContext is not null)
                {
                    await serviceHubContext.DisposeAsync();
                }
                throw;
            }
        }


        public void Dispose()
        {
            (_serviceProvider as IDisposable)?.Dispose();
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