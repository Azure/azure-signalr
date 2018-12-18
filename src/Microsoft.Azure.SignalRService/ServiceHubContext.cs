// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalRService
{
    public class ServiceHubContext : IDisposable
    {
        public IHubClients Clients;
        public IGroupManager Groups;
        public IUserGroupManager UserGroups;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public Task DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}
