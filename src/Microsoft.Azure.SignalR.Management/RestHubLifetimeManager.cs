// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestHubLifetimeManager : HubLifetimeManager<Hub>, IHubLifetimeManagerForUserGroup
    {
        private readonly string _hubName;
        private readonly string _connectionString;

        public RestHubLifetimeManager(ServiceManagerOptions serviceManagerOptions, string hubName)
        {
            _hubName = hubName;
            _connectionString = serviceManagerOptions.ConnectionString;
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            throw new System.NotImplementedException();
        }

        public override Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            throw new System.NotImplementedException();
        }

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        public Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }
    }
}
