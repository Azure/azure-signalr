// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using Azure;

namespace Microsoft.Azure.SignalR.Management
{
    internal class GroupManagerAdapter : GroupManager
    {
        private readonly IServiceHubLifetimeManager _lifetimeManager;

        public GroupManagerAdapter(IServiceHubLifetimeManager lifetimeManager)
        {
            _lifetimeManager = lifetimeManager;
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => _lifetimeManager.AddToGroupAsync(connectionId, groupName, cancellationToken);

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => _lifetimeManager.RemoveFromGroupAsync(connectionId, groupName, cancellationToken);

        public override Task RemoveFromAllGroupsAsync(string connectionId, CancellationToken cancellationToken = default) => _lifetimeManager.RemoveFromAllGroupsAsync(connectionId, cancellationToken);

        public override AsyncPageable<SignalRGroupMember> ListConnectionsInGroup(string groupName, int? top = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                throw new ArgumentException($"'{nameof(groupName)}' cannot be null or whitespace.", nameof(groupName));
            }

            if (top != null && top <= 0)
            {
                throw new ArgumentException($"'{nameof(top)}' must be greater than 0.", nameof(top));
            }

            return _lifetimeManager.ListConnectionsInGroup(groupName, top, cancellationToken);
        }
    }
}
