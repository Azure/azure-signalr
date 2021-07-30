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
using Microsoft.Extensions.Logging;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR.Management
{
    //todo public [ServiceManager]
    internal class ServiceManagerImpl : ServiceManager, IServiceManager
    {
        private readonly RestClientFactory _restClientFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly IReadOnlyCollection<ServiceDescriptor> _services;
        private readonly ServiceEndpoint _endpoint;
        private readonly IServiceEndpointProvider _endpointProvider;

        public ServiceManagerImpl(IReadOnlyCollection<ServiceDescriptor> services, IServiceProvider serviceProvider, RestClientFactory restClientFactory, IServiceEndpointManager endpointManager)
        {
            _services = services;
            _serviceProvider = serviceProvider;
            _restClientFactory = restClientFactory;
            _endpoint = endpointManager.Endpoints.Keys.First();
            _endpointProvider = endpointManager.GetEndpointProvider(_endpoint);
        }

        public async Task<IServiceHubContext> CreateHubContextAsync(string hubName, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
        {
            var builder = new ServiceHubContextBuilder(_services);
            if (loggerFactory != null)
            {
                builder.ConfigureServices(services => services.AddSingleton(loggerFactory));
            }
            var serviceHubContext = await builder.CreateAsync(hubName, cancellationToken);
            return serviceHubContext;
        }

        public override Task<ServiceHubContext> CreateHubContextAsync(string hubName, CancellationToken cancellationToken)
        {
            var builder = new ServiceHubContextBuilder(_services);
            return builder.CreateAsync(hubName, cancellationToken);
        }

        public override void Dispose()
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

        public override Task<bool> IsServiceHealthy(CancellationToken cancellationToken)
        {
            using var restClient = _restClientFactory.Create(_endpoint);
            return restClient.IsServiceHealthy(cancellationToken);
        }
    }
}