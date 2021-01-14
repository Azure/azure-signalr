// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common.RestClients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManager : IServiceManager
    {
        private const string EmptyConnectionStringMessage = "Connection string is null or empty or only contains whitespace.";
        private readonly RestClientFactory _restClientFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly IReadOnlyCollection<ServiceDescriptor> _services;
        private readonly ServiceEndpoint _endpoint;
        private readonly IServiceEndpointProvider _endpointProvider;

        public ServiceManager(IReadOnlyCollection<ServiceDescriptor> services, IServiceProvider serviceProvider, RestClientFactory restClientFactory, IServiceEndpointManager endpointManager, IOptions<ServiceManagerOptions> options)
        {
            _services = services;
            _serviceProvider = serviceProvider;
            _restClientFactory = restClientFactory;
            var connectionString = options.Value.ConnectionString;
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                _endpoint = new ServiceEndpoint(connectionString);
                _endpointProvider = endpointManager.GetEndpointProvider(_endpoint);
            }
        }

        public async Task<IServiceHubContext> CreateHubContextAsync(string hubName, ILoggerFactory loggerFactory = null, CancellationToken cancellationToken = default)
        {
            var servicesPerHub = new ServiceCollection().Add(_services);
            if (loggerFactory != null)
            {
                servicesPerHub.AddSingleton(loggerFactory);
            }
            servicesPerHub.AddSingleton<IServiceConnectionContainer>(sp =>
            sp.GetRequiredService<MultiEndpointConnectionContainerFactory>().GetOrCreate(hubName));
            servicesPerHub.AddSingleton<IServiceHubLifetimeManager>(sp => sp.GetRequiredService<ServiceHubLifetimeManagerFactory>().Create(hubName));
            servicesPerHub.AddSingleton(sp => (HubLifetimeManager<Hub>)sp.GetRequiredService<IServiceHubLifetimeManager>());
            servicesPerHub.AddSingleton<IServiceHubContext>(sp => ActivatorUtilities.CreateInstance<ServiceHubContext>(sp, hubName));

            var serviceProviderForHub = servicesPerHub.BuildServiceProvider();
            var connectionContainer = serviceProviderForHub.GetRequiredService<IServiceConnectionContainer>();
            await connectionContainer.ConnectionInitializedTask.OrTimeout(cancellationToken);
            return serviceProviderForHub.GetRequiredService<IServiceHubContext>();
        }

        public void Dispose()
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }

        public string GenerateClientAccessToken(string hubName, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null)
        {
            if (_endpointProvider == null)
            {
                throw new InvalidOperationException(EmptyConnectionStringMessage);
            }
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
            return _endpointProvider?.GetClientEndpoint(hubName, null, null) ?? throw new InvalidOperationException(EmptyConnectionStringMessage);
        }

        public async Task<bool> IsServiceHealthy(CancellationToken cancellationToken)
        {
            if (_endpoint == null)
            {
                throw new InvalidOperationException(EmptyConnectionStringMessage);
            }
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