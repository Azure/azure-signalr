// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
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
        private const string _appName = "appName";
        private const string _userId = "UserA";
        private const string _groupName = "GroupA";
        private const string _connectionId = "ConnectionA";
        private static readonly string _commonEndpoint = $"{_endpoint}/api/v1/hubs/{_appName.ToLower()}_{_hubName}";

        private static readonly RestApiProvider _restApiProvider = new RestApiProvider(new ServiceEndpoint(_connectionString));

        [Theory]
        [MemberData(nameof(GetTestData))]
        internal async Task RestApiTest(Task<RestApiEndpoint> task, string expectedAudience)
        {
            var api = await task;
            var token = JwtTokenHelper.JwtHandler.ReadJwtToken(api.Token);
            string expectedTokenString = JwtTokenHelper.GenerateExpectedAccessToken(token, expectedAudience, _accessKey);

            Assert.Equal(expectedAudience, api.Audience);
            Assert.Equal(expectedTokenString, api.Token);
        }

        public static IEnumerable<object[]> GetTestData()
        {
            yield return new object[] { _restApiProvider.GetBroadcastEndpointAsync(_appName, _hubName), _commonEndpoint };
            yield return new object[] { _restApiProvider.GetSendToUserEndpointAsync(_appName, _hubName, _userId), $"{_commonEndpoint}/users/{_userId}" };
            yield return new object[] { _restApiProvider.GetSendToGroupEndpointAsync(_appName, _hubName, _groupName), $"{_commonEndpoint}/groups/{_groupName}" };
            yield return new object[] { _restApiProvider.GetUserGroupManagementEndpointAsync(_appName, _hubName, _userId, _groupName), $"{_commonEndpoint}/groups/{_groupName}/users/{_userId}" };
            yield return new object[] { _restApiProvider.GetSendToConnectionEndpointAsync(_appName, _hubName, _connectionId), $"{_commonEndpoint}/connections/{_connectionId}" };
            yield return new object[] { _restApiProvider.GetConnectionGroupManagementEndpointAsync(_appName, _hubName, _connectionId, _groupName), $"{_commonEndpoint}/groups/{_groupName}/connections/{_connectionId}" };
        }
    }
}