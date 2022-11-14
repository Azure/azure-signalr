// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    public abstract class GroupManager : IGroupManager
    {
        public abstract Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default);

        public abstract Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default);

        public abstract Task RemoveFromAllGroupsAsync(string connectionId, CancellationToken cancellationToken);
    }
}