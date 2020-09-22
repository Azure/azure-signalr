// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    internal class MultiEndpointUserGroupManager : IUserGroupManager
    {
        private readonly IEndpointRouter _router;
        private readonly Dictionary<ServiceEndpoint, IUserGroupManager> _userGroupManagerTable;

        internal MultiEndpointUserGroupManager(IEndpointRouter router, Dictionary<ServiceEndpoint, IUserGroupManager> userGroupManagerTable)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _userGroupManagerTable = userGroupManagerTable ?? throw new ArgumentNullException(nameof(userGroupManagerTable));
        }

        public Task AddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return Dispatch(
                userGroupManager => userGroupManager.AddToGroupAsync(userId, groupName, cancellationToken),
                userId,
                groupName);
        }

        public Task AddToGroupAsync(string userId, string groupName, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            return Dispatch(
                userGroupManager => userGroupManager.AddToGroupAsync(userId, groupName, ttl, cancellationToken),
                userId,
                groupName);
        }

        public Task RemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Dispatch(
                userGroupManager => userGroupManager.RemoveFromAllGroupsAsync(userId, cancellationToken),
                userId,
                null);
        }

        public Task RemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            return Dispatch(
                userGroupManager => userGroupManager.RemoveFromGroupAsync(userId, groupName, cancellationToken),
                userId,
                groupName);
        }

        public async Task<bool> IsUserInGroup(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var tasks = GetEndpointsForUserOrGroup(userId, groupName)
                .Select(endpoint => _userGroupManagerTable[endpoint])
                .Select(userGroupManager => userGroupManager.IsUserInGroup(userId, groupName, cancellationToken))
                .ToList();
            var exceptions = new LinkedList<Exception>();
            while (tasks.Count > 0)
            {
                try
                {
                    Task<bool> finishedTask = await Task.WhenAny(tasks);
                    tasks.Remove(finishedTask);
                    bool isUserInGroup = await finishedTask;
                    if (isUserInGroup)
                    {
                        source.Cancel();
                        return true;
                    }
                }
                catch (Exception e)
                {
                    exceptions.AddLast(e);
                }
            }
            if (exceptions.Count > 0)
            {
                throw new AggregateException(exceptions);
            }

            return false;
        }

        private Task Dispatch(Func<IUserGroupManager, Task> func, string userId, string groupName = null)
        {
            return Task.WhenAll(
                GetEndpointsForUserOrGroup(userId, groupName)
                .Select(endpoint => _userGroupManagerTable[endpoint])
                .Select(userGroupManager => func(userGroupManager))
                );
        }

        private IEnumerable<ServiceEndpoint> GetEndpointsForUserOrGroup(string userId, string groupName)
        {
            if (userId == null)
            {
                throw new ArgumentNullException(nameof(userId));
            }
            var endpointsForUser = _router.GetEndpointsForUser(userId, _userGroupManagerTable.Keys);
            if (groupName == null)
            {
                return endpointsForUser;
            }

            var endpointsForGroup = _router.GetEndpointsForGroup(groupName, _userGroupManagerTable.Keys);
            return endpointsForUser.Intersect(endpointsForGroup);
        }
    }
}
