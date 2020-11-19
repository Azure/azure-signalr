// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common.RestClients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Rest;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceManager : IServiceManager
    {
        private readonly ServiceEndpointProvider _endpointProvider;
        private readonly IServerNameProvider _serverNameProvider;
        private readonly ServiceEndpoint _endpoint;
        private readonly RestClientFactory _restClientFactory;
        private readonly ServiceHubContextFactory _serviceHubContextFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly bool _disposeServiceProvider;

        public ServiceManager(IOptions<ServiceManagerContext> context, RestClientFactory restClientFactory, ServiceHubContextFactory serviceHubContextFactory, IServiceProvider serviceProvider)
        {
            _endpoint = context.Value.ServiceEndpoints.Single();//temp solution

            _serverNameProvider = new DefaultServerNameProvider();

            var serviceOptions = Options.Create(new ServiceOptions
            {
                ApplicationName = context.Value.ApplicationName,
                Proxy = context.Value.Proxy
            }).Value;

            _endpointProvider = new ServiceEndpointProvider(_serverNameProvider, _endpoint, serviceOptions, NullLoggerFactory.Instance);

            _restClientFactory = restClientFactory;
            _serviceHubContextFactory = serviceHubContextFactory;
            _serviceProvider = serviceProvider;
            _disposeServiceProvider = context.Value.DisposeServiceProvider;
        }

        public Task<IServiceHubContext> CreateHubContextAsync(string hubName, ILoggerFactory loggerFactory = null, CancellationToken cancellationToken = default) =>
            _serviceHubContextFactory.CreateAsync(hubName, loggerFactory, cancellationToken);

        public void Dispose()
        {
            if (_disposeServiceProvider)
            {
                (_serviceProvider as IDisposable).Dispose();
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