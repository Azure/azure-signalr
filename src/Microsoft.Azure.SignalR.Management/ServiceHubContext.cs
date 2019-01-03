// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalRService
{
    internal class ServiceHubContext : IServiceHubContext
    {
        public IUserGroupManager UserGroups => throw new NotImplementedException();

        public IHubClients Clients => throw new NotImplementedException();

        public IGroupManager Groups => throw new NotImplementedException();

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
