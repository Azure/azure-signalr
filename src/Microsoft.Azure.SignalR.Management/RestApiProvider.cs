// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestApiProvider
    {
        private const string Version = "2022-06-01";
        public const string HealthApiPath = $"api/health?api-version={Version}";

        private readonly RestApiAccessTokenGenerator _restApiAccessTokenGenerator;

        private readonly string _audienceBaseUrl;

        private readonly string _serverEndpoint;

        public RestApiProvider(ServiceEndpoint endpoint)
        {
            _audienceBaseUrl = endpoint.AudienceBaseUrl;
            _serverEndpoint = endpoint.ServerEndpoint.AbsoluteUri;
            _restApiAccessTokenGenerator = new RestApiAccessTokenGenerator(endpoint.AccessKey);
        }

        public async Task<RestApiEndpoint> GetServiceHealthEndpointAsync()
        {
            var url = $"{_serverEndpoint}api/health?api-version={Version}";
            var audience = $"{_audienceBaseUrl}api/health?api-version={Version}";
            var token = await _restApiAccessTokenGenerator.Generate(audience);
            return new RestApiEndpoint(url, token);
        }

        public Task<RestApiEndpoint> GetBroadcastEndpointAsync(string appName, string hubName, TimeSpan? lifetime = null, IReadOnlyList<string> excluded = null)
        {
            var queries = excluded == null ? null : new Dictionary<string, StringValues>() { { "excluded", excluded.ToArray() } };
            return GenerateRestApiEndpointAsync(appName, hubName, "/:send", lifetime, queries);
        }

        public Task<RestApiEndpoint> GetUserGroupManagementEndpointAsync(string appName, string hubName, string userId, string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/users/{Uri.EscapeDataString(userId)}/groups/{Uri.EscapeDataString(groupName)}", lifetime);
        }

        public Task<RestApiEndpoint> GetSendToUserEndpointAsync(string appName, string hubName, string userId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/users/{Uri.EscapeDataString(userId)}/:send", lifetime);
        }

        public Task<RestApiEndpoint> GetSendToGroupEndpointAsync(string appName, string hubName, string groupName, TimeSpan? lifetime = null, IReadOnlyList<string> excluded = null)
        {
            var queries = excluded == null ? null : new Dictionary<string, StringValues>() { { "excluded", excluded.ToArray() } };
            return GenerateRestApiEndpointAsync(appName, hubName, $"/groups/{Uri.EscapeDataString(groupName)}/:send", lifetime, queries);
        }

        public Task<RestApiEndpoint> GetRemoveUserFromAllGroupsAsync(string appName, string hubName, string userId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/users/{Uri.EscapeDataString(userId)}/groups", lifetime);
        }

        public Task<RestApiEndpoint> GetRemoveConnectionFromAllGroupsAsync(string appName, string hubName, string connectionId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/connections/{Uri.EscapeDataString(connectionId)}/groups", lifetime);
        }

        public Task<RestApiEndpoint> GetSendToConnectionEndpointAsync(string appName, string hubName, string connectionId, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/connections/{Uri.EscapeDataString(connectionId)}/:send", lifetime);
        }

        public Task<RestApiEndpoint> GetConnectionGroupManagementEndpointAsync(string appName, string hubName, string connectionId, string groupName, TimeSpan? lifetime = null)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/groups/{Uri.EscapeDataString(groupName)}/connections/{Uri.EscapeDataString(connectionId)}", lifetime);
        }

        public Task<RestApiEndpoint> GetCloseConnectionEndpointAsync(string appName, string hubName, string connectionId, string reason)
        {
            var queries = reason == null ? null : new Dictionary<string, StringValues>() { { "reason", reason } };
            return GenerateRestApiEndpointAsync(appName, hubName, $"/connections/{Uri.EscapeDataString(connectionId)}", queries: queries);
        }

        public Task<RestApiEndpoint> GetCheckConnectionExistsEndpointAsync(string appName, string hubName, string connectionId)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/connections/{Uri.EscapeDataString(connectionId)}");
        }

        public Task<RestApiEndpoint> GetCheckUserExistsEndpointAsync(string appName, string hubName, string user)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/users/{Uri.EscapeDataString(user)}");
        }

        public Task<RestApiEndpoint> GetCheckGroupExistsEndpointAsync(string appName, string hubName, string group)
        {
            return GenerateRestApiEndpointAsync(appName, hubName, $"/groups/{Uri.EscapeDataString(group)}");
        }

        private async Task<RestApiEndpoint> GenerateRestApiEndpointAsync(string appName, string hubName, string pathAfterHub, TimeSpan? lifetime = null, IDictionary<string, StringValues> queries = null)
        {
            var requestPrefixWithHub = $"{_serverEndpoint}api/hubs/{Uri.EscapeDataString(hubName.ToLowerInvariant())}";
            pathAfterHub = string.IsNullOrEmpty(appName)
                ? $"{pathAfterHub}?api-version={Version}"
                : $"{pathAfterHub}?application={Uri.EscapeDataString(appName.ToLowerInvariant())}&api-version={Version}";
            // todo: should be same with `requestPrefixWithHub`, need to confirm with emulator.
            var audiencePrefixWithHub = $"{_audienceBaseUrl}api/hubs/{Uri.EscapeDataString(hubName.ToLowerInvariant())}";
            var token = await _restApiAccessTokenGenerator.Generate($"{audiencePrefixWithHub}{pathAfterHub}", lifetime);
            return new RestApiEndpoint($"{requestPrefixWithHub}{pathAfterHub}", token) { Query = queries };
        }
    }
}