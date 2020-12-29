// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceContext : IServiceContext
    {
        private readonly ServiceHubContextFactory _serviceHubContextFactory;
        private readonly NegotiateProcessor _negotiateProcessor;
        private readonly IServiceProvider _serviceProvider;
        private readonly IServiceEndpointManager _endpointManager;

        public ServiceContext(ServiceHubContextFactory serviceHubContextFactory, NegotiateProcessor negotiateProcessor, IServiceProvider serviceProvider, IServiceEndpointManager endpointManager)
        {
            _serviceHubContextFactory = serviceHubContextFactory;
            _negotiateProcessor = negotiateProcessor;
            _serviceProvider = serviceProvider;
            _endpointManager = endpointManager;
        }

        public Task<IServiceHubContext> CreateHubContextAsync(string hubName, CancellationToken cancellationToken = default)
        {
            return _serviceHubContextFactory.CreateAsync(hubName, null, cancellationToken);
        }

        public Task<NegotiationResponse> GetClientEndpointAsync(string hubName, HttpContext httpContext = null, string userId = null, IList<Claim> claims = null, TimeSpan? lifetime = null, CancellationToken cancellationToken = default)
        {
            return _negotiateProcessor.GetClientEndpointAsync(hubName, httpContext, userId, claims, lifetime, cancellationToken);
        }

        public void Dispose()
        {
            (_serviceProvider as IDisposable)?.Dispose();
        }

        IEnumerable<ServiceEndpoint> IServiceContext.GetServiceEndpoints(string hubName)
        {
            return _endpointManager.GetEndpoints(hubName);
        }
        }
    }
}