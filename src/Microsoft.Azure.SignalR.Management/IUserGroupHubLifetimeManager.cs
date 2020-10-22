﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    internal interface IUserGroupHubLifetimeManager
    {
        Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default);
        
        Task UserAddToGroupAsync(string userId, string groupName, TimeSpan ttl, CancellationToken cancellationToken = default);

        Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default);

        Task UserRemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default);

        Task<bool> IsUserInGroup(string userId, string groupName, CancellationToken cancellationToken = default);
    }
}
