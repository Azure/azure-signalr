// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class HubClientsProxy : IHubClients<IClientProxy>
    {
        private readonly IHubMessageSender _hubMessageSender;
        private readonly string _endpoint;
        private readonly string _accessKey;
        private readonly string _hubName;
        private readonly HubProxyOptions _options;

        public HubClientsProxy(IHubMessageSender hubMessageSender, string endpoint, string accessKey, string hubName, HubProxyOptions options)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (string.IsNullOrEmpty(accessKey))
            {
                throw new ArgumentNullException(nameof(accessKey));
            }

            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }
            _hubMessageSender = hubMessageSender;
            _endpoint = endpoint;
            _accessKey = accessKey;
            _hubName = hubName.ToLower();
            _options = options ?? HubProxyOptions.DefaultHubProxyOptions;

            All = ClientProxyFactory.CreateAllClientsProxy(_hubMessageSender, _endpoint, _options.ApiVersion, _accessKey, _hubName);
        }

        public IClientProxy All { get; }

        public IClientProxy AllExcept(IReadOnlyList<string> excludedIds)
        {
            return ClientProxyFactory.CreateAllClientsExceptProxy(_hubMessageSender, _endpoint, _options.ApiVersion, _accessKey, _hubName,
                excludedIds);
        }

        public IClientProxy Client(string connectionId)
        {
            return ClientProxyFactory.CreateSingleClientProxy(_hubMessageSender, _endpoint, _options.ApiVersion, _accessKey, _hubName,
                connectionId);
        }

        public IClientProxy Clients(IReadOnlyList<string> connectionIds)
        {
            return ClientProxyFactory.CreateMultipleClientProxy(_hubMessageSender, _endpoint, _options.ApiVersion, _accessKey, _hubName,
                connectionIds);
        }

        public IClientProxy Group(string groupName)
        {
            return ClientProxyFactory.CreateSingleGroupProxy(_hubMessageSender, _endpoint, _options.ApiVersion, _accessKey, _hubName,
                groupName);
        }

        public IClientProxy Groups(IReadOnlyList<string> groupNames)
        {
            return ClientProxyFactory.CreateMultipleGroupProxy(_hubMessageSender, _endpoint, _options.ApiVersion, _accessKey, _hubName,
                groupNames);
        }

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludeIds)
        {
            return ClientProxyFactory.CreateSingleGroupExceptProxy(_hubMessageSender, _endpoint, _options.ApiVersion, _accessKey, _hubName,
                groupName, excludeIds);
        }

        public IClientProxy User(string userId)
        {
            return ClientProxyFactory.CreateSingleUserProxy(_hubMessageSender, _endpoint, _options.ApiVersion, _accessKey, _hubName,
                userId);
        }

        public IClientProxy Users(IReadOnlyList<string> userIds)
        {
            return ClientProxyFactory.CreateMultipleUserProxy(_hubMessageSender, _endpoint, _options.ApiVersion, _accessKey, _hubName,
                userIds);
        }
    }
}
