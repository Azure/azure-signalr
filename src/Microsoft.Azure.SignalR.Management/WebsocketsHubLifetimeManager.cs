// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class WebsocketsHubLifetimeManager : ServiceLifetimeManagerCore<Hub>, IHubLifetimeManagerForUserGroup
    {
        public WebsocketsHubLifetimeManager(IServiceConnectionManager<Hub> serviceConnectionManager, IHubProtocolResolver protocolResolver) : base(serviceConnectionManager, protocolResolver)
        {
        }

        public Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
