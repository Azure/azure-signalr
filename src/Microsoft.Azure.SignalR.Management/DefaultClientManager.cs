// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    internal class DefaultClientManager : ClientManager
    {
        private readonly IServiceHubLifetimeManager _lifetimeManager;

        public DefaultClientManager(IServiceHubLifetimeManager lifetimeManager)
        {
            _lifetimeManager = lifetimeManager;
        }

        public override Task CloseConnectionAsync(string connectionId, string reason, CancellationToken cancellationToken)
        {
            return _lifetimeManager.CloseConnectionAsync(connectionId, reason, cancellationToken);
        }
    }
}