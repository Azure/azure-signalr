// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        internal async Task EnableMessageTracingIdInRestApiTest(bool enable)
        {
            var api = await _restApiProvider.GetBroadcastEndpointAsync("app", "hub").WithTracingIdAsync(enable);
            Assert.Equal(enable, api.Query?.ContainsKey(Constants.Headers.AsrsMessageTracingId) ?? false);
            if (enable)
            {
                var id = Convert.ToUInt64(api.Query[Constants.Headers.AsrsMessageTracingId]);
                Assert.Equal(MessageWithTracingIdHelper.Prefix, id);
            }
        }

        public static IEnumerable<object[]> GetTestData() =>
            from context in GetContext()
            from pair in GetTestDataByContext(context)
            select pair;

        private static IEnumerable<(string appName, string hubName, string userId, string groupName, string connectionId)> GetContext()
        {
            yield return ("app", "hub", "userA", "groupA", "connectionA");
            yield return ("APP", "HUB", "USERA", "GROUPA", "CONNECTIONA");
            yield return ("app/x", "hub/x", "user/a", "group/a", "connection/a");
        }

        private static IEnumerable<object[]> GetTestDataByContext((string appName, string hubName, string userId, string groupName, string connectionId) context)
        {
            string commonEndpoint = $"{_endpoint}/api/v1/hubs/{Uri.EscapeDataString(context.appName.ToLowerInvariant())}_{Uri.EscapeDataString(context.hubName.ToLowerInvariant())}";
            yield return new object[] { _restApiProvider.GetBroadcastEndpointAsync(context.appName, context.hubName), commonEndpoint };
            yield return new object[] { _restApiProvider.GetSendToUserEndpointAsync(context.appName, context.hubName, context.userId), $"{commonEndpoint}/users/{Uri.EscapeDataString(context.userId)}" };
            yield return new object[] { _restApiProvider.GetSendToGroupEndpointAsync(context.appName, context.hubName, context.groupName), $"{commonEndpoint}/groups/{Uri.EscapeDataString(context.groupName)}" };
            yield return new object[] { _restApiProvider.GetUserGroupManagementEndpointAsync(context.appName, context.hubName, context.userId, context.groupName), $"{commonEndpoint}/groups/{Uri.EscapeDataString(context.groupName)}/users/{Uri.EscapeDataString(context.userId)}" };
            yield return new object[] { _restApiProvider.GetSendToConnectionEndpointAsync(context.appName, context.hubName, context.connectionId), $"{commonEndpoint}/connections/{Uri.EscapeDataString(context.connectionId)}" };
            yield return new object[] { _restApiProvider.GetConnectionGroupManagementEndpointAsync(context.appName, context.hubName, context.connectionId, context.groupName), $"{commonEndpoint}/groups/{Uri.EscapeDataString(context.groupName)}/connections/{Uri.EscapeDataString(context.connectionId)}" };
        }
    }
}