// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class RestApiProviderFacts: VerifiableLoggedTest
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

        private static readonly RestApiProvider _restApiProvider = new RestApiProvider(_connectionString, _hubName, _appName);

        public RestApiProviderFacts(ITestOutputHelper output): base(output)
        {

        }

        [Theory]
        [MemberData(nameof(GetTestData))]
        public void RestApiTest(string audience, RestApiEndpoint api, string expectedAudience)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug,
                expectedErrors: context => context.EventId == new EventId(2, "EndpointOffline")))
            {
                var token = JwtTokenHelper.JwtHandler.ReadJwtToken(api.Token);
                string expectedTokenString = JwtTokenHelper.GenerateExpectedAccessToken(token, expectedAudience, _accessKey, out var accessKey);

                Assert.Equal(accessKey.Id, _restApiProvider.AccessKey.Id);
                Assert.Equal(expectedAudience, audience);
                Assert.Equal(expectedTokenString, api.Token);
            }
                
        }

        public static IEnumerable<object[]> GetTestData()
        {
            var broadcastApi = _restApiProvider.GetBroadcastEndpoint();
            var sendToUserApi = _restApiProvider.GetSendToUserEndpoint(_userId);
            var sendToGroupApi = _restApiProvider.GetSendToGroupEndpoint(_groupName);
            var groupManagementApi = _restApiProvider.GetUserGroupManagementEndpoint(_userId, _groupName);
            var sendToConnctionsApi = _restApiProvider.GetSendToConnectionEndpoint(_connectionId);
            var connectionGroupManagementApi = _restApiProvider.GetConnectionGroupManagementEndpoint(_connectionId, _groupName);

            yield return new object[] { broadcastApi.Audience, broadcastApi, _commonEndpoint };
            yield return new object[] { sendToUserApi.Audience, sendToUserApi,  $"{_commonEndpoint}/users/{_userId}"};
            yield return new object[] { sendToGroupApi.Audience, sendToGroupApi, $"{_commonEndpoint}/groups/{_groupName}"};
            yield return new object[] { groupManagementApi.Audience, groupManagementApi, $"{_commonEndpoint}/groups/{_groupName}/users/{_userId}"};
            yield return new object[] { sendToConnctionsApi.Audience, sendToConnctionsApi, $"{_commonEndpoint}/connections/{_connectionId}" };
            yield return new object[] { connectionGroupManagementApi.Audience, connectionGroupManagementApi, $"{_commonEndpoint}/groups/{_groupName}/connections/{_connectionId}" };
        }
    }
}