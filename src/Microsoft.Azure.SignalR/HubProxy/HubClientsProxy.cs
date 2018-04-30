// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    internal class HubClientsProxy : IHubClients
    {
        private readonly IHubMessageSender _hubMessageSender;
        private readonly string _hubName;

        public HubClientsProxy(IHubMessageSender hubMessageSender, string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            _hubMessageSender = hubMessageSender ?? throw new ArgumentNullException(nameof(hubMessageSender));
            _hubName = hubName.ToLower();

            All = new ClientProxy(hubMessageSender, $"/hub/{hubName}");
        }

        public IClientProxy All { get; }

        public IClientProxy AllExcept(IReadOnlyList<string> excludedIds)
        {
            return new ClientProxy(_hubMessageSender, $"/hub/{_hubName}", excludedIds);
        }

        public IClientProxy Client(string connectionId)
        {
            return new ClientProxy(_hubMessageSender, $"/hub/{_hubName}/connection/{connectionId}");
        }

        public IClientProxy Clients(IReadOnlyList<string> connectionIds)
        {
            var path = $"/hub/{_hubName}/connections/{string.Join(",", connectionIds)}";
            return new ClientProxy(_hubMessageSender, path);
        }

        public IClientProxy Group(string groupName)
        {
            return new ClientProxy(_hubMessageSender, $"/hub/{_hubName}/group/{groupName}");
        }

        public IClientProxy Groups(IReadOnlyList<string> groupNames)
        {
            var path = $"/hub/{_hubName}/groups/{string.Join(",", groupNames)}";
            return new ClientProxy(_hubMessageSender, path);
        }

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludeIds)
        {
            return new ClientProxy(_hubMessageSender, $"/hub/{_hubName}/group/{groupName}", excludeIds);
        }

        public IClientProxy User(string userId)
        {
            return new ClientProxy(_hubMessageSender, $"/hub/{_hubName}/user/{userId}");
        }

        public IClientProxy Users(IReadOnlyList<string> userIds)
        {
            var path = $"/hub/{_hubName}/users/{string.Join(",", userIds)}";
            return new ClientProxy(_hubMessageSender, path);
        }
    }
}
