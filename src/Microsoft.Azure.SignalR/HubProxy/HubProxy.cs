// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class HubProxy : IHubClients<IClientProxy>
    {
        private readonly HubProxyOptions _options;

        private readonly string _endpoint;
        private readonly string _accessKey;

        public HubProxy(string endpoint, string accessKey, string hubName) : this(endpoint, accessKey, hubName, null)
        {
        }

        public HubProxy(string endpoint, string accessKey, string hubName, HubProxyOptions options)
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

            _endpoint = endpoint;
            _accessKey = accessKey;
            _options = options ?? HubProxyOptions.DefaultHubProxyOptions;

            HubName = hubName.ToLower();
            All = ClientProxyFactory.CreateAllClientsProxy(_endpoint, _options.ApiVersion, _accessKey, HubName);
        }

        public string HubName { get; }

        public IClientProxy All { get; }

        public IClientProxy AllExcept(IReadOnlyList<string> excludedIds)
        {
            return ClientProxyFactory.CreateAllClientsExceptProxy(_endpoint, _options.ApiVersion, _accessKey, HubName,
                excludedIds);
        }

        public IClientProxy Client(string connectionId)
        {
            return ClientProxyFactory.CreateSingleClientProxy(_endpoint, _options.ApiVersion, _accessKey, HubName,
                connectionId);
        }

        public IClientProxy Clients(IReadOnlyList<string> connectionIds)
        {
            return ClientProxyFactory.CreateMultipleClientProxy(_endpoint, _options.ApiVersion, _accessKey, HubName,
                connectionIds);
        }

        public IClientProxy Group(string groupName)
        {
            return ClientProxyFactory.CreateSingleGroupProxy(_endpoint, _options.ApiVersion, _accessKey, HubName,
                groupName);
        }

        public IClientProxy Groups(IReadOnlyList<string> groupNames)
        {
            return ClientProxyFactory.CreateMultipleGroupProxy(_endpoint, _options.ApiVersion, _accessKey, HubName,
                groupNames);
        }

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludeIds)
        {
            return ClientProxyFactory.CreateSingleGroupExceptProxy(_endpoint, _options.ApiVersion, _accessKey, HubName,
                groupName, excludeIds);
        }

        public IClientProxy User(string userId)
        {
            return ClientProxyFactory.CreateSingleUserProxy(_endpoint, _options.ApiVersion, _accessKey, HubName,
                userId);
        }

        public IClientProxy Users(IReadOnlyList<string> userIds)
        {
            return ClientProxyFactory.CreateMultipleUserProxy(_endpoint, _options.ApiVersion, _accessKey, HubName,
                userIds);
        }
    }
}
