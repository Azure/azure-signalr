// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Azure.SignalR.Tests;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class RestApiProviderFacts
    {
        private const string _endpoint = "https://abc";
        private const string _accessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";

        private static readonly string _connectionString = $"Endpoint={_endpoint};AccessKey={_accessKey};Version=1.0;";
        private const string _hubName = "signalrbench";
        private const string _hubPrefix = "prefix";
        private const string _userId = "UserA";
        private const string _groupName = "GroupA";
        private static readonly string _commonEndpoint = $"{_endpoint}/api/v1/hubs/{_hubPrefix}_{_hubName}";

        private static readonly RestApiProvider _restApiProvider = new RestApiProvider(_connectionString, _hubName, _hubPrefix);

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void RestApiTest(string audience, string tokenString, string expectedAudience)
        {
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);
            string expectedTokenString = JwtTokenHelper.GenerateExpectedAccessToken(token, expectedAudience, _accessKey);

            Assert.Equal(expectedAudience, audience);
            Assert.Equal(expectedTokenString, tokenString);
        }

        public static IEnumerable<object[]> GetTestData()
        {
            var broadcastApi = _restApiProvider.GetBroadcastEndpoint();
            var sendToUserApi = _restApiProvider.GetSendToUserEndpoint(_userId);
            var sendToGroupApi = _restApiProvider.GetSendToGroupEndpoint(_groupName);
            var groupManagementApi = _restApiProvider.GetUserGroupManagementEndpoint(_userId, _groupName);

            yield return new object[] { broadcastApi.Audience, broadcastApi.Token, _commonEndpoint };
            yield return new object[] { sendToUserApi.Audience, sendToUserApi.Token,  $"{_commonEndpoint}/users/{_userId}"};
            yield return new object[] { sendToGroupApi.Audience, sendToGroupApi.Token, $"{_commonEndpoint}/groups/{_groupName}"};
            yield return new object[] { groupManagementApi.Audience, groupManagementApi.Token, $"{_commonEndpoint}/groups/{_groupName}/users/{_userId}"};
        }
    }
}