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

            All = ClientProxyFactory.CreateAllClientsProxy(_hubMessageSender, _hubName);
        }

        public IClientProxy All { get; }

        public IClientProxy AllExcept(IReadOnlyList<string> excludedIds)
        {
            return ClientProxyFactory.CreateAllClientsExceptProxy(_hubMessageSender, _hubName,
                excludedIds);
        }

        public IClientProxy Client(string connectionId)
        {
            return ClientProxyFactory.CreateSingleClientProxy(_hubMessageSender, _hubName,
                connectionId);
        }

        public IClientProxy Clients(IReadOnlyList<string> connectionIds)
        {
            return ClientProxyFactory.CreateMultipleClientProxy(_hubMessageSender, _hubName,
                connectionIds);
        }

        public IClientProxy Group(string groupName)
        {
            return ClientProxyFactory.CreateSingleGroupProxy(_hubMessageSender, _hubName,
                groupName);
        }

        public IClientProxy Groups(IReadOnlyList<string> groupNames)
        {
            return ClientProxyFactory.CreateMultipleGroupProxy(_hubMessageSender, _hubName,
                groupNames);
        }

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludeIds)
        {
            return ClientProxyFactory.CreateSingleGroupExceptProxy(_hubMessageSender, _hubName,
                groupName, excludeIds);
        }

        public IClientProxy User(string userId)
        {
            return ClientProxyFactory.CreateSingleUserProxy(_hubMessageSender, _hubName,
                userId);
        }

        public IClientProxy Users(IReadOnlyList<string> userIds)
        {
            return ClientProxyFactory.CreateMultipleUserProxy(_hubMessageSender, _hubName,
                userIds);
        }
    }
}
