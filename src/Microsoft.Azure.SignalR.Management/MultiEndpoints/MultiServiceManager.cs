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
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management.MultiEndpoints
{
    internal class MultiServiceManager : ServiceManagerBase
    {
        private readonly IEndpointRouter _router;
        private readonly ServiceOptions _serviceOptions;
        private readonly Dictionary<ServiceEndpoint, ServiceEndpointProvider> _endpointProviderDict;

        internal MultiServiceManager(ServiceManagerOptions serviceManagerOptions, string productInfo, RestClientFactory restClientFactory, IEndpointRouter router = null) : base(serviceManagerOptions, productInfo, restClientFactory)
        {
            _serviceOptions = Options.Create(new ServiceOptions
            {
                ApplicationName = serviceManagerOptions.ApplicationName,
                Proxy = serviceManagerOptions.Proxy,
                Endpoints = serviceManagerOptions.ServiceEndpoints,
                ConnectionCount = serviceManagerOptions.ConnectionCount
            }).Value;
            _router = router ?? new DefaultEndpointRouter();
            _endpointProviderDict = _serviceOptions.Endpoints.ToDictionary(
                endpoint => endpoint,
                endpoint => new ServiceEndpointProvider(_serverNameProvider, endpoint, _serviceOptions));
        }

        protected override IServiceConnectionContainer GetPersistentConnectionContainer(string hubName, ServiceProvider serviceProvider)
        {
            var serviceConnectionFactory = serviceProvider.GetRequiredService<IServiceConnectionFactory>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var endpointManager = serviceProvider.GetRequiredService<IServiceEndpointManager>();
            Func<HubServiceEndpoint, IServiceConnectionContainer> generator = endpoint => new WeakServiceConnectionContainer(serviceConnectionFactory, _serviceOptions.ConnectionCount, endpoint, loggerFactory.CreateLogger<WeakServiceConnectionContainer>());
            return new MultiEndpointServiceConnectionContainer(hubName, generator, endpointManager, _router, loggerFactory);
        }

        protected override void ConfigurePersistentServiceCollection(ServiceCollection serviceCollection)
        {
            serviceCollection
                .AddSingleton(_serverNameProvider) //required by ServiceEndpointManager
                .AddSingleton<IServiceEndpointManager, ServiceEndpointManager>();
            serviceCollection
                .Configure<ServiceOptions>(options =>
                {
                    options.ApplicationName = _serviceOptions.ApplicationName;
                    options.Proxy = _serviceOptions.Proxy;
                    options.Endpoints = _serviceOptions.Endpoints;
                    options.ConnectionCount = _serviceOptions.ConnectionCount;
                });//required by ServiceEndpointManager
        }

        protected override HubLifetimeManager<Hub> GetTransientHubLifetimeManager(string hubName)
        {
            throw new NotImplementedException();
        }

        public override string GenerateClientAccessToken(string hubName, ServiceEndpoint endpoint, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null)
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
            return _endpointProviderDict[endpoint].GenerateClientAccessTokenAsync(hubName, claimsWithUserId, lifeTime).Result;
        }

        public override ServiceEndpoint GetClientEndpoint(HttpContext httpContext)
        {
            return _router.GetNegotiateEndpoint(httpContext, _serviceOptions.Endpoints);
        }

        public override string GetClientEndpoint(string hubName, ServiceEndpoint endpoint)
        {
            return _endpointProviderDict[endpoint].GetClientEndpoint(hubName, null, null);
        }

        public override Task<bool> IsServiceHealthy(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public override string GenerateClientAccessToken(string hubName, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null)
        {
            throw new NotSupportedException("Please use method GenerateClientAccessToken(string hubName, ServiceEndpoint endpoint, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null) instead");
        }

        public override string GetClientEndpoint(string hubName)
        {
            throw new NotSupportedException("Please use method GetClientEndpoint(string hubName, HttpContext httpContext) instead");
        }

    }
}