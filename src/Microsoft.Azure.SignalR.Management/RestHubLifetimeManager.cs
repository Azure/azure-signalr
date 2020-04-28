// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestHubLifetimeManager : HubLifetimeManager<Hub>, IHubLifetimeManagerForUserGroup
    {
        private const string NullOrEmptyStringErrorMessage = "Argument cannot be null or empty.";
        private static readonly RestClient _restClient = new RestClient();
        private readonly RestApiProvider _restApiProvider;
        private readonly string _productInfo;
        private readonly string _hubName;
        private readonly string _appName;

        public RestHubLifetimeManager(ServiceManagerOptions serviceManagerOptions, string hubName, string productInfo)
        {
            _restApiProvider = new RestApiProvider(serviceManagerOptions.ConnectionString);
            _productInfo = productInfo;
            _appName = serviceManagerOptions.ApplicationName;
            _hubName = hubName;
        }

        public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var api = _restApiProvider.GetConnectionGroupManagementEndpoint(_appName, _hubName, connectionId, groupName);
            return _restClient.SendAsync(api, HttpMethod.Put, _productInfo, handleExpectedResponseAsync: null, cancellationToken: cancellationToken);
        }

        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            throw new NotSupportedException();
        }

        public override Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            throw new NotSupportedException();
        }

        public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var api = _restApiProvider.GetConnectionGroupManagementEndpoint(_appName, _hubName, connectionId, groupName);
            return _restClient.SendAsync(api, HttpMethod.Delete, _productInfo, handleExpectedResponseAsync: null, cancellationToken: cancellationToken);
        }

        public override Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var api = _restApiProvider.GetBroadcastEndpoint(_appName, _hubName);
            return _restClient.SendAsync(api, HttpMethod.Post, _productInfo, methodName, args, handleExpectedResponseAsync: null, cancellationToken: cancellationToken);
        }

        public override Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            var api = _restApiProvider.GetSendToConnectionEndpoint(_appName, _hubName, connectionId);
            return _restClient.SendAsync(api, HttpMethod.Post, _productInfo, methodName, args, handleExpectedResponseAsync: null, cancellationToken: cancellationToken);
        }

        public override async Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(connectionIds.Select(id => SendConnectionAsync(id, methodName, args, cancellationToken)));
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var api = _restApiProvider.GetSendToGroupEndpoint(_appName, _hubName, groupName);
            return _restClient.SendAsync(api, HttpMethod.Post, _productInfo, methodName, args, handleExpectedResponseAsync: null, cancellationToken: cancellationToken);
        }

        public override Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public override async Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            Task all = null;

            try
            {
                await (all = Task.WhenAll(from groupName in groupNames
                                          select SendGroupAsync(groupName, methodName, args, cancellationToken)));
            }
            catch
            {
                throw all.Exception;
            }
        }

        public override Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            var api = _restApiProvider.GetSendToUserEndpoint(_appName, _hubName, userId);
            return _restClient.SendAsync(api, HttpMethod.Post, _productInfo, methodName, args, handleExpectedResponseAsync: null, cancellationToken: cancellationToken);
        }

        public override async Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            Task all = null;

            try
            {
                await (all = Task.WhenAll(from userId in userIds
                                          select SendUserAsync(userId, methodName, args, cancellationToken)));
            }
            catch
            {
                throw all.Exception;
            }
        }

        public Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            ValidateUserIdAndGroupName(userId, groupName);

            var api = _restApiProvider.GetUserGroupManagementEndpoint(_appName, _hubName, userId, groupName);
            return _restClient.SendAsync(api, HttpMethod.Put, _productInfo, handleExpectedResponseAsync: null, cancellationToken: cancellationToken);
        }

        public Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            ValidateUserIdAndGroupName(userId, groupName);

            var api = _restApiProvider.GetUserGroupManagementEndpoint(_appName, _hubName, userId, groupName);
            return _restClient.SendAsync(api, HttpMethod.Delete, _productInfo, handleExpectedResponseAsync: null, cancellationToken: cancellationToken);
        }

        public Task UserRemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default)
        {
            var api = _restApiProvider.GetRemoveUserFromAllGroups(_appName, _hubName, userId);
            return _restClient.SendAsync(api, HttpMethod.Delete, _productInfo, handleExpectedResponseAsync: null, cancellationToken: cancellationToken);
        }

        public async Task<bool> IsUserInGroup(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            var isUserInGroup = false;
            var api = _restApiProvider.GetUserGroupManagementEndpoint(_appName, _hubName, userId, groupName);
            await _restClient.SendAsync(api, HttpMethod.Get, _productInfo, handleExpectedResponse: (request, response) =>
                {
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            isUserInGroup = true;
                            return true;
                        case HttpStatusCode.NotFound:
                            return true;
                        default:
                            return false;
                    }
                }, cancellationToken: cancellationToken);
            return isUserInGroup;
        }

        private static void ValidateUserIdAndGroupName(string userId, string groupName)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }
        }
    }
}
