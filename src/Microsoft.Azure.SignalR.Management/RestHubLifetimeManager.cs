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
using Microsoft.Extensions.Primitives;
using static Microsoft.Azure.SignalR.Constants;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestHubLifetimeManager<THub> : HubLifetimeManager<THub>, IServiceHubLifetimeManager<THub> where THub : Hub
    {
        private const string NullOrEmptyStringErrorMessage = "Argument cannot be null or empty.";
        private const string TtlOutOfRangeErrorMessage = "Ttl cannot be less than 0.";

        private readonly RestClient _restClient;
        private readonly RestApiProvider _restApiProvider;
        private readonly string _productInfo;
        private readonly string _hubName;
        private readonly string _appName;

        public RestHubLifetimeManager(string hubName, ServiceEndpoint endpoint, string productInfo, string appName, RestClient restClient)
        {
            _restApiProvider = new RestApiProvider(endpoint);
            _productInfo = productInfo;
            _appName = appName;
            _hubName = hubName;
            _restClient = restClient;
        }

        public override async Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var api = await _restApiProvider.GetConnectionGroupManagementEndpointAsync(_appName, _hubName, connectionId, groupName);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Put, _productInfo, handleExpectedResponse: static response => FilterExpectedResponse(response, ErrorCodes.ErrorConnectionNotExisted), cancellationToken: cancellationToken);
        }

        public override Task OnConnectedAsync(HubConnectionContext connection)
        {
            throw new NotSupportedException();
        }

        public override Task OnDisconnectedAsync(HubConnectionContext connection)
        {
            throw new NotSupportedException();
        }

        public override async Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var api = await _restApiProvider.GetConnectionGroupManagementEndpointAsync(_appName, _hubName, connectionId, groupName);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Delete, _productInfo, handleExpectedResponse: null, cancellationToken: cancellationToken);
        }

        public async Task RemoveFromAllGroupsAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            var api = await _restApiProvider.GetRemoveConnectionFromAllGroupsAsync(_appName, _hubName, connectionId);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Delete, _productInfo, handleExpectedResponse: null, cancellationToken: cancellationToken);
        }

        public override Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            return SendAllExceptAsync(methodName, args, null, cancellationToken);
        }

        public override async Task SendAllExceptAsync(string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var api = await _restApiProvider.GetBroadcastEndpointAsync(_appName, _hubName, excluded: excludedConnectionIds);
            await _restClient.SendMessageWithRetryAsync(api, HttpMethod.Post, _productInfo, methodName, args, handleExpectedResponse: null, cancellationToken: cancellationToken);
        }

        public override async Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            var api = await _restApiProvider.GetSendToConnectionEndpointAsync(_appName, _hubName, connectionId);
            await _restClient.SendMessageWithRetryAsync(api, HttpMethod.Post, _productInfo, methodName, args, handleExpectedResponse: null, cancellationToken: cancellationToken);
        }

        public override async Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(connectionIds.Select(id => SendConnectionAsync(id, methodName, args, cancellationToken)));
        }

        public override Task SendGroupAsync(string groupName, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            return SendGroupExceptAsync(groupName, methodName, args, null, cancellationToken);
        }

        public override async Task SendGroupExceptAsync(string groupName, string methodName, object[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }

            var api = await _restApiProvider.GetSendToGroupEndpointAsync(_appName, _hubName, groupName, excluded: excludedConnectionIds);
            await _restClient.SendMessageWithRetryAsync(api, HttpMethod.Post, _productInfo, methodName, args, handleExpectedResponse: null, cancellationToken: cancellationToken);
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

        public override async Task SendUserAsync(string userId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }

            var api = await _restApiProvider.GetSendToUserEndpointAsync(_appName, _hubName, userId);
            await _restClient.SendMessageWithRetryAsync(api, HttpMethod.Post, _productInfo, methodName, args, handleExpectedResponse: null, cancellationToken: cancellationToken);
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

        public async Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            ValidateUserIdAndGroupName(userId, groupName);

            var api = await _restApiProvider.GetUserGroupManagementEndpointAsync(_appName, _hubName, userId, groupName);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Put, _productInfo, handleExpectedResponse: null, cancellationToken: cancellationToken);
        }

        public async Task UserAddToGroupAsync(string userId, string groupName, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            ValidateUserIdAndGroupName(userId, groupName);

            if (ttl < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ttl), TtlOutOfRangeErrorMessage);
            }
            var api = await _restApiProvider.GetUserGroupManagementEndpointAsync(_appName, _hubName, userId, groupName);
            api.Query = new Dictionary<string, StringValues>
            {
                ["ttl"] = ((int)ttl.TotalSeconds).ToString(),
            };
            await _restClient.SendWithRetryAsync(api, HttpMethod.Put, _productInfo, handleExpectedResponse: null, cancellationToken: cancellationToken);
        }

        public async Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            ValidateUserIdAndGroupName(userId, groupName);

            var api = await _restApiProvider.GetUserGroupManagementEndpointAsync(_appName, _hubName, userId, groupName);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Delete, _productInfo, handleExpectedResponse: null, cancellationToken: cancellationToken);
        }

        public async Task UserRemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default)
        {
            var api = await _restApiProvider.GetRemoveUserFromAllGroupsAsync(_appName, _hubName, userId);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Delete, _productInfo, handleExpectedResponse: null, cancellationToken: cancellationToken);
        }

        public async Task<bool> IsUserInGroup(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            var isUserInGroup = false;
            var api = await _restApiProvider.GetUserGroupManagementEndpointAsync(_appName, _hubName, userId, groupName);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Get, _productInfo, handleExpectedResponse: response =>
                {
                    isUserInGroup = response.StatusCode == HttpStatusCode.OK;
                    return FilterExpectedResponse(response, ErrorCodes.InfoUserNotInGroup);
                }, cancellationToken: cancellationToken);
            return isUserInGroup;
        }

        public async Task CloseConnectionAsync(string connectionId, string reason, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }
            var api = await _restApiProvider.GetCloseConnectionEndpointAsync(_appName, _hubName, connectionId, reason);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Delete, _productInfo, handleExpectedResponse: static response => FilterExpectedResponse(response, ErrorCodes.WarningConnectionNotExisted), cancellationToken: cancellationToken);
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

        public async Task<bool> ConnectionExistsAsync(string connectionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }
            var exists = false;
            var api = await _restApiProvider.GetCheckConnectionExistsEndpointAsync(_appName, _hubName, connectionId);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Head, _productInfo, handleExpectedResponse: response =>
            {
                exists = response.StatusCode == HttpStatusCode.OK;
                return FilterExpectedResponse(response, ErrorCodes.WarningConnectionNotExisted);
            }, cancellationToken: cancellationToken);
            return exists;
        }

        public async Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
            }
            var exists = false;
            var api = await _restApiProvider.GetCheckUserExistsEndpointAsync(_appName, _hubName, userId);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Head, _productInfo, handleExpectedResponse: response =>
            {
                exists = response.StatusCode == HttpStatusCode.OK;
                return FilterExpectedResponse(response, ErrorCodes.WarningUserNotExisted);
            }, cancellationToken: cancellationToken);
            return exists;
        }

        public async Task<bool> GroupExistsAsync(string groupName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
            }
            var exists = false;
            var api = await _restApiProvider.GetCheckGroupExistsEndpointAsync(_appName, _hubName, groupName);
            await _restClient.SendWithRetryAsync(api, HttpMethod.Head, _productInfo, handleExpectedResponse: response =>
            {
                exists = response.StatusCode == HttpStatusCode.OK;
                return FilterExpectedResponse(response, ErrorCodes.WarningGroupNotExisted);
            }, cancellationToken: cancellationToken);
            return exists;
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private static bool FilterExpectedResponse(HttpResponseMessage response, string expectedErrorCode) =>
            response.IsSuccessStatusCode
            || (response.StatusCode == HttpStatusCode.NotFound && response.Headers.TryGetValues(Headers.MicrosoftErrorCode, out var errorCodes) && errorCodes.First().Equals(expectedErrorCode, StringComparison.OrdinalIgnoreCase));
    }
}