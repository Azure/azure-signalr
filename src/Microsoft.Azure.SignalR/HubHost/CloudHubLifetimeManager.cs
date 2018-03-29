// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public abstract class CloudHubLifetimeManager<THub> : HubLifetimeManager<THub> where THub : Hub
    {
        // The difference of following methods from original HubLifetimeManger is that they all accept the original client connection Id.
        public abstract Task SendAllAsyncFromCloud(string methodName, object[] args, string cloudConnectionId);

        public abstract Task SendAllExceptAsyncFromCloud(string methodName, object[] args, IReadOnlyList<string> excludedIds, string cloudConnectionId);

        public abstract Task SendConnectionsAsyncFromCloud(IReadOnlyList<string> connectionIds, string methodName, object[] args, string cloudConnectionId);

        public abstract Task SendGroupAsyncFromCloud(string groupName, string methodName, object[] args, string cloudConnectionId);

        public abstract Task SendGroupExceptAsyncFromCloud(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedIds, string cloudConnectionId);

        public abstract Task SendGroupsAsyncFromCloud(IReadOnlyList<string> groupNames, string methodName, object[] args, string cloudConnectionId);

        public abstract Task SendUserAsyncFromCloud(string userId, string methodName, object[] args, string cloudConnectionId);

        public abstract Task SendUsersAsyncFromCloud(IReadOnlyList<string> userIds, string methodName, object[] args, string cloudConnectionId);
    }
}
