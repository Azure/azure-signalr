// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class GroupManagerAdapter : GroupManager
    {
        private readonly IGroupManager _groupManager;

        public GroupManagerAdapter(IGroupManager groupManager)
        {
            _groupManager = groupManager;
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => _groupManager.AddToGroupAsync(connectionId, groupName, cancellationToken);

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => _groupManager.RemoveFromGroupAsync(connectionId, groupName, cancellationToken);
    }
}