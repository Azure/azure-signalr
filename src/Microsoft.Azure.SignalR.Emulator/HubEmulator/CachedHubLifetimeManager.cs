// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal class CachedHubLifetimeManager<THub> : DefaultHubLifetimeManager<THub>, IHubLifetimeManager where THub : Hub
    {
        private readonly IDynamicHubContextStore _store;
        private readonly string _hub;
        public HubConnectionStore Connections { get; } = new HubConnectionStore();

        public CachedHubLifetimeManager(IDynamicHubContextStore store, ILogger<CachedHubLifetimeManager<THub>> logger) : base(logger)
        {
            _store = store;
            _hub = typeof(THub).Name;
        }

        /// <inheritdoc />
        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            if (_store.TryGetLifetimeContext(_hub, out var context))
            {
                var userGroup = context.UserGroupManager;
                userGroup.OnConnectionOpenning(connection);
            }
            Connections.Add(connection);
            return base.OnConnectedAsync(connection);
        }

        /// <inheritdoc />
        public override Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            if (_store.TryGetLifetimeContext(_hub, out var context))
            {
                var userGroup = context.UserGroupManager;
                userGroup.OnConnectionClosing(connection);
            }

            Connections.Remove(connection);
            return base.OnDisconnectedAsync(connection);
        }
    }
}
