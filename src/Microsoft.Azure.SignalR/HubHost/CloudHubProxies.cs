// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class UserProxy<THub> : Microsoft.AspNetCore.SignalR.IClientProxy where THub : Hub
    {
        private readonly string _userId;
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private readonly string _cloudConnectionId;

        public UserProxy(HubHostLifetimeManager<THub> lifetimeManager,
            string userId, string cloudConnectionId)
        {
            _lifetimeManager = lifetimeManager;
            _userId = userId;
            _cloudConnectionId = cloudConnectionId;
        }

        public Task SendCoreAsync(string method, params object[] args)
        {
            return _lifetimeManager.SendUserAsyncFromCloud(_userId, method, args, _cloudConnectionId);
        }
    }

    public class MultipleUserProxy<THub> : Microsoft.AspNetCore.SignalR.IClientProxy where THub : Hub
    {
        private readonly IReadOnlyList<string> _userIds;
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private readonly string _cloudConnectionId;

        public MultipleUserProxy(HubHostLifetimeManager<THub> lifetimeManager,
            IReadOnlyList<string> userIds, string cloudConnectionId)
        {
            _lifetimeManager = lifetimeManager;
            _userIds = userIds;
            _cloudConnectionId = cloudConnectionId;
        }

        public Task SendCoreAsync(string method, params object[] args)
        {
            return _lifetimeManager.SendUsersAsyncFromCloud(_userIds, method, args, _cloudConnectionId);
        }
    }

    public class GroupProxy<THub> : Microsoft.AspNetCore.SignalR.IClientProxy where THub : Hub
    {
        private readonly string _groupName;
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private readonly string _cloudConnectionId;

        public GroupProxy(HubHostLifetimeManager<THub> lifetimeManager, string groupName, string cloudConnectionId)
        {
            _lifetimeManager = lifetimeManager;
            _groupName = groupName;
            _cloudConnectionId = cloudConnectionId;
        }

        public Task SendCoreAsync(string method, params object[] args)
        {
            return _lifetimeManager.SendGroupAsyncFromCloud(_groupName, method, args, _cloudConnectionId);
        }
    }

    public class MultipleGroupProxy<THub> : Microsoft.AspNetCore.SignalR.IClientProxy where THub : Hub
    {
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private IReadOnlyList<string> _groupNames;
        private readonly string _cloudConnectionId;

        public MultipleGroupProxy(HubHostLifetimeManager<THub> lifetimeManager,
            IReadOnlyList<string> groupNames, string cloudConnectionId)
        {
            _lifetimeManager = lifetimeManager;
            _groupNames = groupNames;
            _cloudConnectionId = cloudConnectionId;
        }

        public Task SendCoreAsync(string method, params object[] args)
        {
            return _lifetimeManager.SendGroupsAsyncFromCloud(_groupNames, method, args, _cloudConnectionId);
        }
    }

    public class GroupExceptProxy<THub> : Microsoft.AspNetCore.SignalR.IClientProxy where THub : Hub
    {
        private readonly string _groupName;
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private readonly IReadOnlyList<string> _excludedIds;
        private readonly string _cloudConnectionId;

        public GroupExceptProxy(HubHostLifetimeManager<THub> lifetimeManager, string groupName,
            IReadOnlyList<string> excludedIds, string cloudConnectionId)
        {
            _lifetimeManager = lifetimeManager;
            _groupName = groupName;
            _excludedIds = excludedIds;
            _cloudConnectionId = cloudConnectionId;
        }

        public Task SendCoreAsync(string method, params object[] args)
        {
            return _lifetimeManager.SendGroupExceptAsyncFromCloud(_groupName, method, args, _excludedIds, _cloudConnectionId);
        }
    }

    public class AllClientProxy<THub> : Microsoft.AspNetCore.SignalR.IClientProxy where THub : Hub
    {
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private readonly string _cloudConnectionId;

        public AllClientProxy(HubHostLifetimeManager<THub> lifetimeManager, string cloudConnectionId)
        {
            _lifetimeManager = lifetimeManager;
            _cloudConnectionId = cloudConnectionId;
        }

        public Task SendCoreAsync(string method, object[] args)
        {
            return _lifetimeManager.SendAllAsyncFromCloud(method, args, _cloudConnectionId);
        }
    }

    public class AllClientsExceptProxy<THub> : Microsoft.AspNetCore.SignalR.IClientProxy where THub : Hub
    {
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private readonly string _cloudConnectionId;
        private IReadOnlyList<string> _excludedIds;

        public AllClientsExceptProxy(HubHostLifetimeManager<THub> lifetimeManager,
            IReadOnlyList<string> excludedIds, string cloudConnectionId)
        {
            _lifetimeManager = lifetimeManager;
            _cloudConnectionId = cloudConnectionId;
            _excludedIds = excludedIds;
        }

        public Task SendCoreAsync(string method, object[] args)
        {
            return _lifetimeManager.SendAllExceptAsyncFromCloud(method, args, _excludedIds, _cloudConnectionId);
        }
    }

    public class SingleClientProxy<THub> : Microsoft.AspNetCore.SignalR.IClientProxy where THub : Hub
    {
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private readonly string _connectionId;

        public SingleClientProxy(HubHostLifetimeManager<THub> lifetimeManager, string connectionId)
        {
            _lifetimeManager = lifetimeManager;
            _connectionId = connectionId;
        }

        public Task SendCoreAsync(string method, object[] args)
        {
            return _lifetimeManager.SendConnectionAsync(_connectionId, method, args);
        }
    }

    public class MultipleClientProxy<THub> : Microsoft.AspNetCore.SignalR.IClientProxy where THub : Hub
    {
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;
        private readonly string _cloudConnectionId;
        private IReadOnlyList<string> _connectionIds;

        public MultipleClientProxy(HubHostLifetimeManager<THub> lifetimeManager,
            IReadOnlyList<string> connectionIds, string cloudConnectionId)
        {
            _lifetimeManager = lifetimeManager;
            _cloudConnectionId = cloudConnectionId;
            _connectionIds = connectionIds;
        }

        public Task SendCoreAsync(string method, object[] args)
        {
            return _lifetimeManager.SendConnectionsAsyncFromCloud(_connectionIds, method, args, _cloudConnectionId);
        }
    }

    public class GroupManager<THub> : Microsoft.AspNetCore.SignalR.IGroupManager where THub : Hub
    {
        private readonly HubHostLifetimeManager<THub> _lifetimeManager;

        public GroupManager(HubHostLifetimeManager<THub> lifetimeManager)
        {
            _lifetimeManager = lifetimeManager;
        }

        public Task AddAsync(string connectionId, string groupName)
        {
            return _lifetimeManager.AddGroupAsync(connectionId, groupName);
        }

        public Task RemoveAsync(string connectionId, string groupName)
        {
            return _lifetimeManager.RemoveGroupAsync(connectionId, groupName);
        }
    }
}
