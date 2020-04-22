// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestApiProvider
    {
        private const string Version = "v1";
        private readonly RestApiAccessTokenGenerator _restApiAccessTokenGenerator;
        private readonly string _baseEndpoint;
        private readonly int? _port;

        public RestApiProvider(string connectionString)
        {
            string key;
            (_baseEndpoint, key, _, _port) = ConnectionStringParser.Parse(connectionString);
            _restApiAccessTokenGenerator = new RestApiAccessTokenGenerator(new AccessKey(key));
        }

        private string GetPrefixedHubName(string applicationName, string hubName)
        {
            return string.IsNullOrEmpty(applicationName) ? hubName.ToLower() : $"{applicationName.ToLower()}_{hubName.ToLower()}";
        }

        public RestApiEndpoint GetServiceHealthEndpoint()
        {
            var url = $"{_baseEndpoint}/api/{Version}/health";
            var token = _restApiAccessTokenGenerator.Generate(url);
            return new RestApiEndpoint(url, token);
        }

        public RestApiEndpoint GetBroadcastEndpoint(string appName, string hubName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint(appName, hubName, "", lifetime);
        }

        public RestApiEndpoint GetUserGroupManagementEndpoint(string appName, string hubName, string userId, string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint(appName, hubName, $"/groups/{groupName}/users/{userId}", lifetime);
        }

        public RestApiEndpoint GetSendToUserEndpoint(string appName, string hubName, string userId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint(appName, hubName, $"/users/{userId}", lifetime);
        }

        public RestApiEndpoint GetSendToGroupEndpoint(string appName, string hubName, string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint(appName, hubName, $"/groups/{groupName}", lifetime);
        }

        public RestApiEndpoint GetRemoveUserFromAllGroups(string appName, string hubName, string userId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint(appName, hubName, $"/users/{userId}/groups", lifetime);
        }

        public RestApiEndpoint GetSendToConnectionEndpoint(string appName, string hubName, string connectionId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint(appName, hubName, $"/connections/{connectionId}", lifetime);
        }

        public RestApiEndpoint GetConnectionGroupManagementEndpoint(string appName, string hubName, string connectionId, string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpoint(appName, hubName, $"/groups/{groupName}/connections/{connectionId}", lifetime);
        }

        private RestApiEndpoint GenerateRestApiEndpoint(string appName, string hubName, string pathAfterHub, TimeSpan? lifetime = null)
        {
            var requestPrefixWithHub = _port == null ? $"{_baseEndpoint}/api/{Version}/hubs/{GetPrefixedHubName(appName, hubName)}" : $"{_baseEndpoint}:{_port}/api/v1/hubs/{GetPrefixedHubName(appName, hubName)}";
            var audiencePrefixWithHub = $"{_baseEndpoint}/api/{Version}/hubs/{GetPrefixedHubName(appName, hubName)}";
            var token = _restApiAccessTokenGenerator.Generate($"{audiencePrefixWithHub}{pathAfterHub}", lifetime);
            return new RestApiEndpoint($"{requestPrefixWithHub}{pathAfterHub}", token);
        }
    }
}
