// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.SignalRService
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
