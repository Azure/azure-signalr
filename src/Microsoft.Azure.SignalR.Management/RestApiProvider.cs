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

        public RestApiProvider(string connectionString, string hubName)
        {
            string accessKey;
            (_baseEndpoint, accessKey, _, _) = ConnectionStringParser.Parse(connectionString);
            _hubName = hubName;
            _restApiAccessTokenGenerator = new RestApiAccessTokenGenerator(accessKey);
        }

        public RestApiEndpoint GetBroadcastEndpoint(TimeSpan? lifetime = null)
        {
            var api = $"{_baseEndpoint}/api/v1/hubs/{_hubName}";
            return GenerateRestApiEndpoint(api, lifetime);
        }

        public RestApiEndpoint GetUserGroupManagementEndpoint(string userId, string groupName, TimeSpan? lifetime = null)
        {
            var api = $"{_baseEndpoint}/api/v1/hubs/{_hubName}/groups/{groupName}/users/{userId}";
            return GenerateRestApiEndpoint(api, lifetime);
        }

        public RestApiEndpoint GetSendToUserEndpoint(string userId, TimeSpan? lifetime = null)
        {
            var api = $"{_baseEndpoint}/api/v1/hubs/{_hubName}/users/{userId}";
            return GenerateRestApiEndpoint(api, lifetime);
        }

        public RestApiEndpoint GetSendToGroupEndpoint(string groupName, TimeSpan? lifetime = null)
        {
            var api = $"{_baseEndpoint}/api/v1/hubs/{_hubName}/groups/{groupName}";
            return GenerateRestApiEndpoint(api, lifetime);
        }

        private RestApiEndpoint GenerateRestApiEndpoint(string api, TimeSpan? lifetime = null)
        {
            var token = _restApiAccessTokenGenerator.Generate(api, lifetime);
            return new RestApiEndpoint(api, token);
        }
    }
}
