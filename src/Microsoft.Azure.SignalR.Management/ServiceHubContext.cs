// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common.ServiceConnections;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContext : IServiceHubContext
    {
        private IHubContext<Hub> _hubContext;
        private ServiceProvider _serviceProvider;

        public IHubClients Clients => _hubContext.Clients;

        public IGroupManager Groups => _hubContext.Groups;

        public IUserGroupManager UserGroups { get; }

        // for test only
        internal IServiceConnectionContainer ConnectionContainer { get; }

        public ServiceHubContext(IHubContext<Hub> hubContext, IHubLifetimeManagerForUserGroup lifetimeManager, ServiceProvider serviceProvider)
        {
            _hubContext = hubContext;
            UserGroups = new UserGroupsManager(lifetimeManager);
            _serviceProvider = serviceProvider;
            ConnectionContainer = _serviceProvider.GetService<IServiceConnectionContainer>();
        }

        public async Task DisposeAsync()
        {
            await StopConnectionAsync();
            _serviceProvider?.Dispose();
        }

        private Task StopConnectionAsync()
        {
            var serviceConnectionManager = _serviceProvider.GetService<IServiceConnectionManager<Hub>>();
            if (serviceConnectionManager == null)
            {
                return Task.CompletedTask;
            }
            return serviceConnectionManager.StopAsync();
        }
    }
}