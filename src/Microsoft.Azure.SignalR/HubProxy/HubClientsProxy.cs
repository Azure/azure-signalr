// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class HubClientsProxy : IHubClients
    {
        private readonly IHubMessageSender _hubMessageSender;
        private readonly string _encodedHubName;

        public HubClientsProxy(IHubMessageSender hubMessageSender, string hubName)
        {
            CheckNullString(hubName, nameof(hubName));

            _hubMessageSender = hubMessageSender ?? throw new ArgumentNullException(nameof(hubMessageSender));
            _encodedHubName = WebUtility.UrlEncode(hubName.ToLower());

            All = new ClientProxy(hubMessageSender, $"/hub/{_encodedHubName}");
        }

        public IClientProxy All { get; }

        public IClientProxy AllExcept(IReadOnlyList<string> excludedIds)
        {
            return new ClientProxy(_hubMessageSender, $"/hub/{_encodedHubName}", excludedIds);
        }

        public IClientProxy Client(string connectionId)
        {
            CheckNullString(connectionId, nameof(connectionId));

            return new ClientProxy(_hubMessageSender, $"/hub/{_encodedHubName}/connection/{connectionId}");
        }

        public IClientProxy Clients(IReadOnlyList<string> connectionIds)
        {
            CheckEmptyList(connectionIds, nameof(connectionIds));

            var encodedConnectionList = WebUtility.UrlEncode(string.Join(",", connectionIds));
            var path = $"/hub/{_encodedHubName}/connections/{encodedConnectionList}";
            return new ClientProxy(_hubMessageSender, path);
        }

        public IClientProxy Group(string groupName)
        {
            CheckNullString(groupName, nameof(groupName));

            var encodedGroupName = WebUtility.UrlEncode(groupName);
            return new ClientProxy(_hubMessageSender, $"/hub/{_encodedHubName}/group/{encodedGroupName}");
        }

        public IClientProxy Groups(IReadOnlyList<string> groupNames)
        {
            CheckEmptyList(groupNames, nameof(groupNames));

            var encodedGroupList = WebUtility.UrlEncode(string.Join(",", groupNames));
            var path = $"/hub/{_encodedHubName}/groups/{encodedGroupList}";
            return new ClientProxy(_hubMessageSender, path);
        }

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludeIds)
        {
            CheckNullString(groupName, nameof(groupName));

            var encodedGroupName = WebUtility.UrlEncode(groupName);
            return new ClientProxy(_hubMessageSender, $"/hub/{_encodedHubName}/group/{encodedGroupName}", excludeIds);
        }

        public IClientProxy User(string userId)
        {
            CheckNullString(userId, nameof(userId));

            var encodedUserId = WebUtility.UrlEncode(userId);
            return new ClientProxy(_hubMessageSender, $"/hub/{_encodedHubName}/user/{encodedUserId}");
        }

        public IClientProxy Users(IReadOnlyList<string> userIds)
        {
            CheckEmptyList(userIds, nameof(userIds));

            var encodedUserList = WebUtility.UrlEncode(string.Join(",", userIds));
            var path = $"/hub/{_encodedHubName}/users/{encodedUserList}";
            return new ClientProxy(_hubMessageSender, path);
        }

        private static void CheckNullString(string value, string name)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentNullException(name);
            }
        }

        private static void CheckEmptyList(IReadOnlyList<string> list, string name)
        {
            if (list == null || list.Count == 0)
            {
                throw new ArgumentNullException(name);
            }
        }
    }
}
