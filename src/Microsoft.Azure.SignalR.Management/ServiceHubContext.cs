// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContext : IServiceHubContext
    {
        private readonly IHubContext<Hub> _hubContext;
        private readonly IServiceHubLifetimeManager _lifetimeManager;
        private readonly ServiceHubContextFactory _factory;
        private readonly IServiceEndpointManager _endpointManager;
        private readonly string _hubName;

        internal ServiceProvider ServiceProvider { get; }

        public IHubClients Clients => _hubContext.Clients;

        public IGroupManager Groups => _hubContext.Groups;

        public IUserGroupManager UserGroups { get; }

        public ServiceHubContext(IServiceEndpointManager endpointManager, IHubContext<Hub> hubContext, IServiceHubLifetimeManager lifetimeManager, ServiceProvider serviceProvider, ServiceHubContextFactory factory, string hubName)
        {
            _hubContext = hubContext;
            _lifetimeManager = lifetimeManager;
            UserGroups = new UserGroupsManager(lifetimeManager);
            ServiceProvider = serviceProvider;
            _factory = factory;
            _endpointManager = endpointManager;
            _hubName = hubName;
        }

        IEnumerable<ServiceEndpoint> IServiceHubContext.Endpoints => _endpointManager.GetEndpoints(_hubName);

        IServiceHubContext IServiceHubContext.WithEndpoints(IEnumerable<ServiceEndpoint> endpoints)
        {
            return _factory.Create(_hubName, endpoints);
        }

        public async Task DisposeAsync()
        {
            await _lifetimeManager.DisposeAsync();
            ServiceProvider?.Dispose();
        }
    }
}