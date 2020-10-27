// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    internal class UserGroupsManager : IUserGroupManager
    {
        private IUserGroupHubLifetimeManager _lifetimeManager;

        public UserGroupsManager(IUserGroupHubLifetimeManager lifetimeManager)
        {
            _lifetimeManager = lifetimeManager;
        }

        public Task AddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserAddToGroupAsync(userId, groupName, cancellationToken);
        }

        public Task AddToGroupAsync(string userId, string groupName, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserAddToGroupAsync(userId, groupName, ttl, cancellationToken);
        }

        public Task RemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserRemoveFromAllGroupsAsync(userId, cancellationToken);
        }

        public Task<bool> IsUserInGroup(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.IsUserInGroup(userId, groupName, cancellationToken);
        }

        public Task RemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserRemoveFromGroupAsync(userId, groupName, cancellationToken);
        }
    }
}
