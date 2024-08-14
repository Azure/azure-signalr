// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    /// <summary>
    /// Per hub per context
    /// </summary>
    internal class DynamicHubContext
    {
        public DynamicHubContext(
            Type hubType,
            IHubClients clientManager,
            IHubLifetimeManager lifetimeManager,
            ConnectionHandler connectionHandler)
        {
            HubType = hubType;
            ClientManager = clientManager;
            LifetimeManager = lifetimeManager;
            ConnectionHandler = connectionHandler;
        }

        protected DynamicHubContext()
        {
        }

        public Type HubType { get; }
        public IHubClients ClientManager { get; }
        public IHubLifetimeManager LifetimeManager { get; }
        public ConnectionHandler ConnectionHandler { get; }
        public virtual GroupManager UserGroupManager { get; } = new GroupManager();
    }
}
