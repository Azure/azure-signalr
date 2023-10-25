// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

internal class ServiceManagerImpl : ServiceManager, IServiceManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RestClient _restClient;
    private readonly IReadOnlyCollection<ServiceDescriptor> _services;
    private readonly RestApiProvider _restApiEndpointProvider;
    private readonly IServiceEndpointProvider _serviceEndpointProvider;

    public ServiceManagerImpl(IReadOnlyCollection<ServiceDescriptor> services, IServiceProvider serviceProvider, RestClient restClient, IServiceEndpointManager endpointManager)
    {
        _services = services;
        _serviceProvider = serviceProvider;
        _restClient = restClient;
        var endpoint = endpointManager.Endpoints.Keys.First();
        _serviceEndpointProvider = endpointManager.GetEndpointProvider(endpoint);
        _restApiEndpointProvider = new RestApiProvider(endpoint);
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

    public override Task<ServiceHubContext<T>> CreateHubContextAsync<T>(string hubName, CancellationToken cancellation)
    {
        var builder = new ServiceHubContextBuilder(_services);
        return builder.CreateAsync<T>(hubName, cancellation);
    }

    public override void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }

    public string GenerateClientAccessToken(string hubName, string? userId = null, IList<Claim>? claims = null, TimeSpan? lifeTime = null)
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
        return _serviceEndpointProvider.GenerateClientAccessTokenAsync(hubName, claimsWithUserId, lifeTime).Result;
    }

    public string GetClientEndpoint(string hubName)
    {
        return _serviceEndpointProvider.GetClientEndpoint(hubName, null, null);
    }

    public override async Task<bool> IsServiceHealthy(CancellationToken cancellationToken)
    {
        var api = await _restApiEndpointProvider.GetServiceHealthEndpointAsync();
        var isHealthy = false;
        await _restClient.SendAsync(api, HttpMethod.Head, handleExpectedResponse: response =>
        {
            if (response.IsSuccessStatusCode)
            {
                isHealthy = true;
                return true;
            }
            else if (response.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
            {
                isHealthy = false;
                return true;
            }
            return false;
        });
        return isHealthy;
    }
}