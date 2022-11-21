// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class GroupManagerAdapter : GroupManager
    {
        private readonly IHubLifetimeManager _lifetimeManager;

        public GroupManagerAdapter(IHubLifetimeManager lifetimeManager)
        {
            _lifetimeManager = lifetimeManager;
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => _lifetimeManager.AddToGroupAsync(connectionId, groupName, cancellationToken);

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => _lifetimeManager.RemoveFromGroupAsync(connectionId, groupName, cancellationToken);

        public override Task RemoveFromAllGroupsAsync(string connectionId, CancellationToken cancellationToken = default) => _lifetimeManager.RemoveFromAllGroupsAsync(connectionId, cancellationToken);
    }
}