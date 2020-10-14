// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Common.RestClients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR.Management
{
    internal class SingleServiceManager : ServiceManagerBase
    {
        private readonly ServiceEndpointProvider _endpointProvider;
        private readonly ServiceEndpoint _endpoint;

        internal SingleServiceManager(ServiceManagerOptions serviceManagerOptions, string productInfo, RestClientFactory restClientFactory) : base(serviceManagerOptions, productInfo, restClientFactory)
        {
            _endpoint = serviceManagerOptions.ServiceEndpoint;

            var serviceOptions = Options.Create(new ServiceOptions
            {
                ApplicationName = _serviceManagerOptions.ApplicationName,
                Proxy = serviceManagerOptions.Proxy
            }).Value;

            _endpointProvider = new ServiceEndpointProvider(_serverNameProvider, _endpoint, serviceOptions);
        }

        public override string GenerateClientAccessToken(string hubName, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null)
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

        public override string GetClientEndpoint(string hubName)
        {
            return _endpointProvider.GetClientEndpoint(hubName, null, null);
        }

        public override async Task<bool> IsServiceHealthy(CancellationToken cancellationToken)
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

        protected override void ConfigurePersistentServiceCollection(ServiceCollection services)
        {/*do nothing*/}

        protected override IServiceConnectionContainer GetPersistentConnectionContainer(string hubName, ServiceProvider serviceProvider)
        {
            var serviceConnectionFactory = serviceProvider.GetRequiredService<IServiceConnectionFactory>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            return new WeakServiceConnectionContainer(
                            serviceConnectionFactory,
                            _serviceManagerOptions.ConnectionCount,
                            new HubServiceEndpoint(hubName, _endpointProvider, _endpoint),
                            loggerFactory.CreateLogger(nameof(WeakServiceConnectionContainer)));
        }

        protected override HubLifetimeManager<Hub> GetTransientHubLifetimeManager(string hubName)
        {
            return new RestHubLifetimeManager(_serviceManagerOptions, hubName, _productInfo);
        }

        public override string GenerateClientAccessToken(string hubName, ServiceEndpoint endpoint, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null)
        {
            throw new NotSupportedException("please use method GenerateClientAccessToken(string hubName, string userId = null, IList<Claim> claims = null, TimeSpan? lifeTime = null) instead");
        }

        public override ServiceEndpoint GetClientEndpoint(HttpContext httpContext)
        {
            throw new NotSupportedException("please use method GetClientEndpoint(string hubName) instead");
        }

        public override string GetClientEndpoint(string hubName, ServiceEndpoint endpoint)
        {
            throw new NotSupportedException("please use method GetClientEndpoint(string hubName) instead");
        }
    }
}