﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Models;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestHubLifetimeManager : HubLifetimeManager<Hub>, IHubLifetimeManagerForUserGroup
    {
        private const string NullOrEmptyStringErrorMessage = "Argument cannot be null or empty.";
        private const string TtlOutOfRangeErrorMessage = "Ttl cannot be less than 0.";

        private readonly IServiceApi _serviceApi;

        private readonly string _hubName;
        private readonly string _appName;

        public RestHubLifetimeManager(ServiceManagerOptions serviceManagerOptions, string hubName, string productInfo)
        {
            _appName = serviceManagerOptions.ApplicationName;
            _hubName = hubName;
            _serviceApi = GeneratedRestClient.Build(serviceManagerOptions.ConnectionString, productInfo).ServiceApi;
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

            return _serviceApi.AddConnectionToGroupAsync(GetPrefixedHubName(_appName, _hubName), groupName, connectionId, cancellationToken);

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

            return _serviceApi.RemoveConnectionFromGroupAsync(GetPrefixedHubName(_appName, _hubName), groupName, connectionId, cancellationToken);
        }

        public override Task SendAllAsync(string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            var payload = new PayloadMessage(methodName, args.ToList());
            return _serviceApi.BroadcastAsync(GetPrefixedHubName(_appName, _hubName), payload, cancellationToken: cancellationToken);
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

            var payload = new PayloadMessage(methodName, args.ToList());
            return _serviceApi.SendToConnectionAsync(GetPrefixedHubName(_appName, _hubName), connectionId, payload, cancellationToken);
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

            var payload = new PayloadMessage(methodName, args.ToList());
            return _serviceApi.GroupBroadcastAsync(GetPrefixedHubName(_appName, _hubName), groupName, payload, cancellationToken: cancellationToken);
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

            var payload = new PayloadMessage(methodName, args.ToList());
            return _serviceApi.SendToUserAsync(GetPrefixedHubName(_appName, _hubName), userId, payload, cancellationToken);
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

            return _serviceApi.AddUserToGroupAsync(GetPrefixedHubName(_appName, _hubName), groupName, userId, cancellationToken: cancellationToken);
        }

        public Task UserAddToGroupAsync(string userId, string groupName, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            ValidateUserIdAndGroupName(userId, groupName);

            if (ttl < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(ttl), TtlOutOfRangeErrorMessage);
            }
            return _serviceApi.AddUserToGroupAsync(GetPrefixedHubName(_appName, _hubName), groupName, userId, (int)ttl.TotalSeconds, cancellationToken);
        }

        public Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            ValidateUserIdAndGroupName(userId, groupName);

            return _serviceApi.RemoveUserFromGroupAsync(GetPrefixedHubName(_appName, _hubName), groupName, userId, cancellationToken);
        }

        public Task UserRemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default)
        {
            return _serviceApi.RemoveUserFromAllGroupsAsync(GetPrefixedHubName(_appName, _hubName), userId, cancellationToken);
        }

        public async Task<bool> IsUserInGroup(string userId, string groupName, CancellationToken cancellationToken = default)
        {
            var httpOperationResponse = await _serviceApi.CheckUserExistenceInGroupWithHttpMessagesAsync(GetPrefixedHubName(_appName, _hubName), groupName, userId, cancellationToken: cancellationToken);
            return httpOperationResponse.Response.StatusCode switch
            {
                HttpStatusCode.OK => true,
                HttpStatusCode.NotFound => false,
                _ => false
            };
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

        private string GetPrefixedHubName(string applicationName, string _hubName)
        {
            return string.IsNullOrEmpty(applicationName) ? _hubName.ToLower() : $"{applicationName.ToLower()}_{_hubName.ToLower()}";
        }
    }
}
