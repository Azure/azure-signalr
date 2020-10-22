// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContext : IServiceHubContext
    {
        private readonly IHubContext<Hub> _hubContext;
        private readonly IServiceHubLifetimeManager _lifetimeManager;

        internal ServiceProvider ServiceProvider { get; }

        public IHubClients Clients => _hubContext.Clients;

        public IGroupManager Groups => _hubContext.Groups;

        public IUserGroupManager UserGroups { get; }

        public ServiceHubContext(IHubContext<Hub> hubContext, IServiceHubLifetimeManager lifetimeManager, ServiceProvider serviceProvider)
        {
            _hubContext = hubContext;
            _lifetimeManager = lifetimeManager;
            UserGroups = new UserGroupsManager(lifetimeManager);
            ServiceProvider = serviceProvider;
        }

        public async Task DisposeAsync()
        {
            await _lifetimeManager.DisposeAsync();
            ServiceProvider?.Dispose();
        }
    }
}