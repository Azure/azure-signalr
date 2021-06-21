// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    public abstract class UserGroupManager : IUserGroupManager
    {
        public abstract Task AddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default);

        public abstract Task AddToGroupAsync(string userId, string groupName, TimeSpan ttl, CancellationToken cancellationToken = default);

        public abstract Task<bool> IsUserInGroup(string userId, string groupName, CancellationToken cancellationToken = default);

        public abstract Task RemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default);

        public abstract Task RemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default);
    }
}