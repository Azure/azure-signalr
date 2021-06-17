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

        public override Task<bool> CheckIfConnectionExistsAsync(string connectionId, CancellationToken cancellationToken) => _lifetimeManager.CheckIfConnectionExistsAsync(connectionId, cancellationToken);

        public override Task<bool> CheckIfGroupExistsAsync(string groupName, CancellationToken cancellationToken) => _lifetimeManager.CheckIfGroupExistsAsync(groupName, cancellationToken);

        public override Task<bool> CheckIfUserExistsAsync(string userId, CancellationToken cancellationToken) => _lifetimeManager.CheckIfUserExistsAsync(userId, cancellationToken);
    }
}