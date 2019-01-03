﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    internal class UserGroupsManager : IUserGroupManager
    {
        private IHubLifetimeManagerForUserGroup _lifetimeManager;

        public UserGroupsManager(IHubLifetimeManagerForUserGroup lifetimeManager)
        {
            _lifetimeManager = lifetimeManager;
        }

        public Task AddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserAddToGroupAsync(userId, groupName, cancellationToken);
        }

        public Task RemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return _lifetimeManager.UserRemoveFromGroupAsync(userId, groupName, cancellationToken);
        }
    }
}
