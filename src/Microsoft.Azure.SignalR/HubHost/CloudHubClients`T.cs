// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    // TODO.
    // If SignalR provides sending context, for example, connectionID, here we can
    // remove this class. See https://github.com/aspnet/SignalR/issues/1767
    public class CloudHubClients<THub, T> : Microsoft.AspNetCore.SignalR.IHubClients<T> where THub : Hub
    {
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private readonly string _cloudConnectionId;

        public CloudHubClients(HubHostLifetimeManager<THub> lifetimeManager, string cloudConnectionId)
        {
            _lifetimeManager = lifetimeManager;
            _cloudConnectionId = cloudConnectionId;
            All = TypedClientBuilder<T>.Build(new AllClientProxy<THub>(_lifetimeManager, cloudConnectionId));
        }

        public T All { get; }

        public T AllExcept(IReadOnlyList<string> excludedIds)
        {
            return TypedClientBuilder<T>.Build(new AllClientsExceptProxy<THub>(_lifetimeManager, excludedIds, _cloudConnectionId));
        }

        public virtual T Client(string connectionId)
        {
            return TypedClientBuilder<T>.Build(new SingleClientProxy<THub>(_lifetimeManager, connectionId));
        }

        public T Clients(IReadOnlyList<string> connectionIds)
        {
            return TypedClientBuilder<T>.Build(new MultipleClientProxy<THub>(_lifetimeManager, connectionIds, _cloudConnectionId));
        }

        public virtual T Group(string groupName)
        {
            return TypedClientBuilder<T>.Build(new GroupProxy<THub>(_lifetimeManager, groupName, _cloudConnectionId));
        }

        public T GroupExcept(string groupName, IReadOnlyList<string> excludeIds)
        {
            return TypedClientBuilder<T>.Build(new GroupExceptProxy<THub>(_lifetimeManager, groupName, excludeIds, _cloudConnectionId));
        }

        public T Groups(IReadOnlyList<string> groupNames)
        {
            return TypedClientBuilder<T>.Build(new MultipleGroupProxy<THub>(_lifetimeManager, groupNames, _cloudConnectionId));
        }

        public virtual T User(string userId)
        {
            return TypedClientBuilder<T>.Build(new UserProxy<THub>(_lifetimeManager, userId, _cloudConnectionId));
        }

        public virtual T Users(IReadOnlyList<string> userIds)
        {
            return TypedClientBuilder<T>.Build(new MultipleUserProxy<THub>(_lifetimeManager, userIds, _cloudConnectionId));
        }
    }
}
