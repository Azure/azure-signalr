// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

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

        public async Task<RestApiEndpoint> GetServiceHealthEndpointAsync()
        {
            var port = _port == null ? "" : $":{_port}";
            var url = $"{_baseEndpoint}{port}/api/{Version}/health";
            var audience = $"{_baseEndpoint}/api/{Version}/health";
            var token = await _restApiAccessTokenGenerator.Generate(audience);
            return new RestApiEndpoint(url, token);
        }

        public Task<RestApiEndpoint> GetBroadcastEndpointAsync(string appName, string hubName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, "", lifetime);
        }

        public Task<RestApiEndpoint> GetUserGroupManagementEndpointAsync(string appName, string hubName, string userId, string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/groups/{groupName}/users/{userId}", lifetime);
        }

        public Task<RestApiEndpoint> GetSendToUserEndpointAsync(string appName, string hubName, string userId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/users/{userId}", lifetime);
        }

        public Task<RestApiEndpoint> GetSendToGroupEndpointAsync(string appName, string hubName, string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/groups/{groupName}", lifetime);
        }

        public Task<RestApiEndpoint> GetRemoveUserFromAllGroupsAsync(string appName, string hubName, string userId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/users/{userId}/groups", lifetime);
        }

        public Task<RestApiEndpoint> GetSendToConnectionEndpointAsync(string appName, string hubName, string connectionId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/connections/{connectionId}", lifetime);
        }

        public Task<RestApiEndpoint> GetConnectionGroupManagementEndpointAsync(string appName, string hubName, string connectionId, string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/groups/{groupName}/connections/{connectionId}", lifetime);
        }

        private async Task<RestApiEndpoint> GenerateRestApiEndpointAsync(string appName, string hubName, string pathAfterHub, TimeSpan? lifetime = null)
        {
            var requestPrefixWithHub = _port == null ? $"{_baseEndpoint}/api/{Version}/hubs/{GetPrefixedHubName(appName, hubName)}" : $"{_baseEndpoint}:{_port}/api/v1/hubs/{GetPrefixedHubName(appName, hubName)}";
            var audiencePrefixWithHub = $"{_baseEndpoint}/api/{Version}/hubs/{GetPrefixedHubName(appName, hubName)}";
            var token = await _restApiAccessTokenGenerator.Generate($"{audiencePrefixWithHub}{pathAfterHub}", lifetime);
            return new RestApiEndpoint($"{requestPrefixWithHub}{pathAfterHub}", token);
        }
    }
}
