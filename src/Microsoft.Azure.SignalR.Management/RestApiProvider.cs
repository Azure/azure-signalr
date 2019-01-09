// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestApiProvider
    {
        private const string _version = "/api/v1";
        private readonly RestApiAccessTokenGenerator _restApiAccessTokenGenerator;
        private readonly string _baseEndpoint;
        private readonly string _hubName;

        public RestApiProvider(string connectionString, string hubName)
        {
            (_baseEndpoint, _, _, _) = ConnectionStringParser.Parse(connectionString);
            _hubName = hubName;
            _restApiAccessTokenGenerator = new RestApiAccessTokenGenerator(connectionString);
        }

        public RestApiEndpoint Broadcast(TimeSpan? lifetime = null)
        {
            var api = UrlPrefix() + AddHubName();
            return GenerateRestApiEndpoint(api, lifetime);
        }

        public RestApiEndpoint UserGroupManagement(string userId, string groupName, TimeSpan? lifetime = null)
        {
            var api = UrlPrefix() + AddHubName() + AddGroupName(groupName) + AddUserId(userId);
            return GenerateRestApiEndpoint(api, lifetime);
        }

        public RestApiEndpoint SendToUser(string userId, TimeSpan? lifetime = null)
        {
            var api = UrlPrefix() + AddHubName() + AddUserId(userId);
            return GenerateRestApiEndpoint(api, lifetime);
        }

        public RestApiEndpoint SendToGroup(string groupName, TimeSpan? lifetime = null)
        {
            var api = UrlPrefix() + AddHubName() + AddGroupName(groupName);
            return GenerateRestApiEndpoint(api, lifetime);
        }

        private RestApiEndpoint GenerateRestApiEndpoint(string api, TimeSpan? lifetime = null)
        {
            var token = _restApiAccessTokenGenerator.Generate(api, lifetime);
            return new RestApiEndpoint(api, token);
        }

        private static string AddUserId(string userId)
        {
            return "/users/" + userId;
        }

        private static string AddGroupName(string groupName)
        {
            return "/groups/" + groupName;
        }

        private string UrlPrefix()
        {
            return _baseEndpoint + _version;
        }

        private string AddHubName()
        {
            return "/hubs/" + _hubName;
        }
    }
}
