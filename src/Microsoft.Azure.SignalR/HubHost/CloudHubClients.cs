// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class CloudHubClients<THub> : Microsoft.AspNetCore.SignalR.IHubClients where THub : Hub
    {
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private readonly string _cloudConnectionId;

        public CloudHubClients(HubHostLifetimeManager<THub> lifetimeManager, string cloudConnectionId)
        {
            _lifetimeManager = lifetimeManager;
            _cloudConnectionId = cloudConnectionId;
            All = new AllClientProxy<THub>(_lifetimeManager, _cloudConnectionId);
        }

        public AspNetCore.SignalR.IClientProxy All { get; }

        public AspNetCore.SignalR.IClientProxy AllExcept(IReadOnlyList<string> excludedIds)
        {
            return new AllClientsExceptProxy<THub>(_lifetimeManager, excludedIds, _cloudConnectionId);
        }

        public AspNetCore.SignalR.IClientProxy Client(string connectionId)
        {
            return new SingleClientProxy<THub>(_lifetimeManager, connectionId);
        }

        public AspNetCore.SignalR.IClientProxy Clients(IReadOnlyList<string> connectionIds)
        {
            return new MultipleClientProxy<THub>(_lifetimeManager, connectionIds, _cloudConnectionId);
        }

        public AspNetCore.SignalR.IClientProxy Group(string groupName)
        {
            return new GroupProxy<THub>(_lifetimeManager, groupName, _cloudConnectionId);
        }

        public AspNetCore.SignalR.IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludeIds)
        {
            return new GroupExceptProxy<THub>(_lifetimeManager, groupName, excludeIds, _cloudConnectionId);
        }

        public AspNetCore.SignalR.IClientProxy Groups(IReadOnlyList<string> groupNames)
        {
            return new MultipleGroupProxy<THub>(_lifetimeManager, groupNames, _cloudConnectionId);
        }

        public AspNetCore.SignalR.IClientProxy User(string userId)
        {
            return new UserProxy<THub>(_lifetimeManager, userId, _cloudConnectionId);
        }

        public AspNetCore.SignalR.IClientProxy Users(IReadOnlyList<string> userIds)
        {
            return new MultipleUserProxy<THub>(_lifetimeManager, userIds, _cloudConnectionId);
        }
    }
}
