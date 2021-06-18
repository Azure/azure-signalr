// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ClientManagerAdapter : ClientManager
    {
        private readonly IServiceHubLifetimeManager _lifetimeManager;

        public ClientManagerAdapter(IServiceHubLifetimeManager lifetimeManager)
        {
            _lifetimeManager = lifetimeManager;
        }

        public override Task CloseConnectionAsync(string connectionId, string reason, CancellationToken cancellationToken) => _lifetimeManager.CloseConnectionAsync(connectionId, reason, cancellationToken);

        public override Task<bool> ConnectionExistsAsync(string connectionId, CancellationToken cancellationToken) => _lifetimeManager.ConnectionExistsAsync(connectionId, cancellationToken);

        public override Task<bool> GroupExistsAsync(string groupName, CancellationToken cancellationToken) => _lifetimeManager.GroupExistsAsync(groupName, cancellationToken);

        public override Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken) => _lifetimeManager.UserExistsAsync(userId, cancellationToken);
    }
}