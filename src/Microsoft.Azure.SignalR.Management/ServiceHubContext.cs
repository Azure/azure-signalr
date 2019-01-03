// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContext : IServiceHubContext
    {
        private IHubContext<Hub> _hubContext;

        public IHubClients Clients => _hubContext.Clients;

        public IGroupManager Groups => _hubContext.Groups;

        public IUserGroupManager UserGroups { get; }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public ServiceHubContext(IHubContext<Hub> hubContext, IHubLifetimeManagerForUserGroup lifetimeManager)
        {
            _hubContext = hubContext;
            UserGroups = new UserGroupsManager(lifetimeManager);
        }
    }
}