// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    internal class UserGroupsManagerAdapter : UserGroupManager
    {
        private readonly IUserGroupHubLifetimeManager _lifetimeManager;

        public UserGroupsManagerAdapter(IUserGroupHubLifetimeManager lifetimeManager)
        {
            _lifetimeManager = lifetimeManager;
        }

        public override Task AddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserAddToGroupAsync(userId, groupName, cancellationToken);
        }

        public override Task AddToGroupAsync(string userId, string groupName, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserAddToGroupAsync(userId, groupName, ttl, cancellationToken);
        }

        public override Task RemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserRemoveFromAllGroupsAsync(userId, cancellationToken);
        }

        public override Task<bool> IsUserInGroup(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.IsUserInGroup(userId, groupName, cancellationToken);
        }

        public override Task RemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserRemoveFromGroupAsync(userId, groupName, cancellationToken);
        }
    }
}