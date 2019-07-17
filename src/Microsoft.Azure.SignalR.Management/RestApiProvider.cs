// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestApiProvider
    {
        private readonly RestApiAccessTokenGenerator _restApiAccessTokenGenerator;
        private readonly string _baseEndpoint;
        private readonly string _hubName;
        private readonly string _appName;
        private readonly string _requestPrefix;
        private readonly string _audiencePrefix;
        private readonly int? _port;

        public RestApiProvider(string connectionString, string hubName, string appName)
        {
            string accessKey;
            (_baseEndpoint, accessKey, _, _port) = ConnectionStringParser.Parse(connectionString);
            _hubName = hubName;
            _appName = appName;
            _restApiAccessTokenGenerator = new RestApiAccessTokenGenerator(accessKey);
            _requestPrefix = _port == null ? $"{_baseEndpoint}/api/v1/hubs/{GetPrefixedHubName(_appName, _hubName)}" : $"{_baseEndpoint}:{_port}/api/v1/hubs/{GetPrefixedHubName(_appName, _hubName)}";
            _audiencePrefix = $"{_baseEndpoint}/api/v1/hubs/{GetPrefixedHubName(_appName, _hubName)}";
        }

        private string GetPrefixedHubName(string applicationName, string hubName)
        {
            return string.IsNullOrEmpty(applicationName) ? hubName.ToLower() : $"{applicationName.ToLower()}_{hubName.ToLower()}";
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

        public RestApiEndpoint GetRemoveUserFromAllGroups(string userId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint($"/users/{userId}/groups", lifetime);
        }

        public RestApiEndpoint GetSendToConnectionEndpoint(string connectionId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint($"/connections/{connectionId}", lifetime);
        }

        public RestApiEndpoint GetConnectionGroupManagementEndpoint(string connectionId, string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint($"/groups/{groupName}/connections/{connectionId}", lifetime);
        }

        private RestApiEndpoint GenerateRestApiEndpoint(string path, TimeSpan? lifetime = null)
        {
            var token = _restApiAccessTokenGenerator.Generate($"{_audiencePrefix}{path}", lifetime);
            return new RestApiEndpoint($"{_requestPrefix}{path}", token);
        }
    }
}
