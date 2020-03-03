﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContext : IServiceHubContext
    {
        private readonly IHubContext<Hub> _hubContext;
        
        internal ServiceProvider ServiceProvider { get; }

        public IHubClients Clients => _hubContext.Clients;

        public IGroupManager Groups => _hubContext.Groups;

        public IUserGroupManager UserGroups { get; }

        public ServiceHubContext(IHubContext<Hub> hubContext, IHubLifetimeManagerForUserGroup lifetimeManager, ServiceProvider serviceProvider)
        {
            _hubContext = hubContext;
            UserGroups = new UserGroupsManager(lifetimeManager);
            ServiceProvider = serviceProvider;
        }

        public async Task DisposeAsync()
        {
            await StopConnectionAsync();
            ServiceProvider?.Dispose();
        }

        private Task StopConnectionAsync()
        {
            var serviceConnectionManager = ServiceProvider.GetService<IServiceConnectionManager<Hub>>();
            if (serviceConnectionManager == null)
            {
                return Task.CompletedTask;
            }
            return serviceConnectionManager.StopAsync();
        }
    }
}