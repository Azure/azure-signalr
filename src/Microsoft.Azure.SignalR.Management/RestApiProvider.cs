// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestApiProvider
    {
        private const string Version = "/api/v1";
        private const string HubCollection = "/hubs/";
        private const string UserCollection = "/users/";
        private const string GroupCollection = "/groups/";
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
            var api = GetCommonPrefix().ToString();
            return GenerateRestApiEndpoint(api, lifetime);
        }

        public RestApiEndpoint GetUserGroupManagementEndpoint(string userId, string groupName, TimeSpan? lifetime = null)
        {
            var api = GetCommonPrefix().Append(GroupCollection).Append(groupName).Append(UserCollection).Append(userId).ToString();
            return GenerateRestApiEndpoint(api, lifetime);
        }

        public RestApiEndpoint GetSendToUserEndpoint(string userId, TimeSpan? lifetime = null)
        {
            var api = GetCommonPrefix().Append(UserCollection).Append(userId).ToString();
            return GenerateRestApiEndpoint(api, lifetime);
        }

        public RestApiEndpoint GetSendToGroupEndpoint(string groupName, TimeSpan? lifetime = null)
        {
            var api = GetCommonPrefix().Append(GroupCollection).Append(groupName).ToString();
            return GenerateRestApiEndpoint(api, lifetime);
        }

        private RestApiEndpoint GenerateRestApiEndpoint(string api, TimeSpan? lifetime = null)
        {
            var token = _restApiAccessTokenGenerator.Generate(api, lifetime);
            return new RestApiEndpoint(api, token);
        }

        private StringBuilder GetCommonPrefix()
        {
            return new StringBuilder().Append(_baseEndpoint).Append(Version).Append(HubCollection).Append(_hubName);
        }
    }
}
