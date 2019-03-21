﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestApiProvider
    {
        private readonly RestApiAccessTokenGenerator _restApiAccessTokenGenerator;
        private readonly string _baseEndpoint;
        private readonly string _hubName;
        private readonly string _hubPrefix;
        private readonly string _requestPrefix;
        private readonly string _audiencePrefix;
        private readonly int? _port;

        public RestApiProvider(string connectionString, string hubName, string hubPrefix)
        {
            string accessKey;
            (_baseEndpoint, accessKey, _, _port) = ConnectionStringParser.Parse(connectionString);
            _hubName = hubName;
            _hubPrefix = hubPrefix;
            _restApiAccessTokenGenerator = new RestApiAccessTokenGenerator(accessKey);
            if (string.IsNullOrEmpty(_hubPrefix))
            {
                _requestPrefix = _port == null ? $"{_baseEndpoint}/api/v1/hubs/{_hubName}" : $"{_baseEndpoint}:{_port}/api/v1/hubs/{_hubName}";
                _audiencePrefix = $"{_baseEndpoint}/api/v1/hubs/{_hubName}";
            }
            else
            {
                _requestPrefix = _port == null ? $"{_baseEndpoint}/api/v1/hubs/{_hubPrefix}_{_hubName}" : $"{_baseEndpoint}:{_port}/api/v1/hubs/{_hubPrefix}_{_hubName}";
                _audiencePrefix = $"{_baseEndpoint}/api/v1/hubs/{_hubPrefix}_{_hubName}";
            }
        }

        public RestApiEndpoint GetBroadcastEndpoint(TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint("", lifetime);
        }

        public RestApiEndpoint GetUserGroupManagementEndpoint(string userId, string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint($"/groups/{groupName}/users/{userId}", lifetime);
        }

        public RestApiEndpoint GetSendToUserEndpoint(string userId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint($"/users/{userId}", lifetime);
        }

        public RestApiEndpoint GetSendToGroupEndpoint(string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint($"/groups/{groupName}", lifetime);
        }

        private RestApiEndpoint GenerateRestApiEndpoint(string path, TimeSpan? lifetime = null)
        {
            var token = _restApiAccessTokenGenerator.Generate($"{_audiencePrefix}{path}", lifetime);
            return new RestApiEndpoint($"{_requestPrefix}{path}", token);
        }
    }
}
