// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContext : IInternalServiceHubContext
    {
        private readonly string _hubName;
        private readonly IHubContext<Hub> _hubContext;
        private readonly IServiceHubLifetimeManager _lifetimeManager;
        private readonly NegotiateProcessor _negotiateProcessor;
        private readonly IServiceEndpointManager _endpointManager;

        internal IServiceProvider ServiceProvider { get; }

        public IHubClients Clients => _hubContext.Clients;

        public IGroupManager Groups => _hubContext.Groups;

        public IUserGroupManager UserGroups { get; }

        public ServiceHubContext(string hubName, IHubContext<Hub> hubContext, IServiceHubLifetimeManager lifetimeManager, IServiceProvider serviceProvider, NegotiateProcessor negotiateProcessor, IServiceEndpointManager endpointManager)
        {
            _hubName = hubName;
            _hubContext = hubContext;
            _lifetimeManager = lifetimeManager;
            UserGroups = new UserGroupsManager(lifetimeManager);
            ServiceProvider = serviceProvider;
            _negotiateProcessor = negotiateProcessor;
            _endpointManager = endpointManager;
        }

        Task<NegotiationResponse> IInternalServiceHubContext.NegotiateAsync(HttpContext httpContext, string userId, IList<Claim> claims, TimeSpan? lifetime, bool isDiagnosticClient, CancellationToken cancellationToken)
        {
            return _negotiateProcessor.NegotiateAsync(_hubName, httpContext, userId, claims, lifetime, isDiagnosticClient, cancellationToken);
        }

        IEnumerable<ServiceEndpoint> IInternalServiceHubContext.GetServiceEndpoints() => _endpointManager.GetEndpoints(_hubName);

        public async Task DisposeAsync()
        {
            await _lifetimeManager.DisposeAsync();
            (ServiceProvider as IDisposable)?.Dispose();
        }
    }
}