// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Tests;

using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests;

public class RestApiProviderFacts
{
    private const string Endpoint = "https://abc";

    private const string AccessKey = "fake_key";

    private const string ConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";

    private static readonly RestApiProvider RestApiProvider = new(new ServiceEndpoint(ConnectionString));

    public static IEnumerable<object[]> GetTestData()
    {
        return from context in GetContext()
               from pair in GetTestDataByContext(context)
               select pair;
    }

    [Theory]
    [MemberData(nameof(GetTestData))]
    internal async Task RestApiTest(Task<RestApiEndpoint> task, string expectedAudience)
    {
        var api = await task;
        var token = JwtTokenHelper.JwtHandler.ReadJwtToken(api.Token);
        var expectedTokenString = JwtTokenHelper.GenerateExpectedAccessToken(token, expectedAudience, AccessKey);

        Assert.Equal(expectedAudience, api.Audience);
        Assert.Equal(expectedTokenString, api.Token);
    }

    private static IEnumerable<(string appName, string hubName, string userId, string groupName, string connectionId)> GetContext()
    {
        yield return ("app", "hub", "userA", "groupA", "connectionA");
        yield return ("APP", "HUB", "USERA", "GROUPA", "CONNECTIONA");
        yield return ("app/x", "hub/x", "user/a", "group/a", "connection/a");
    }

    private static IEnumerable<object[]> GetTestDataByContext((string appName, string hubName, string userId, string groupName, string connectionId) context)
    {
        var commonEndpoint = $"{Endpoint}/api/hubs/{Uri.EscapeDataString(context.hubName.ToLowerInvariant())}";
        var commonQueryString = $"application={Uri.EscapeDataString(context.appName.ToLowerInvariant())}&api-version=2022-06-01";
        yield return new object[] { RestApiProvider.GetBroadcastEndpointAsync(context.appName, context.hubName), $"{commonEndpoint}/:send?{commonQueryString}" };
        yield return new object[] { RestApiProvider.GetSendToUserEndpointAsync(context.appName, context.hubName, context.userId), $"{commonEndpoint}/users/{Uri.EscapeDataString(context.userId)}/:send?{commonQueryString}" };
        yield return new object[] { RestApiProvider.GetSendToGroupEndpointAsync(context.appName, context.hubName, context.groupName), $"{commonEndpoint}/groups/{Uri.EscapeDataString(context.groupName)}/:send?{commonQueryString}" };
        yield return new object[] { RestApiProvider.GetUserGroupManagementEndpointAsync(context.appName, context.hubName, context.userId, context.groupName), $"{commonEndpoint}/users/{Uri.EscapeDataString(context.userId)}/groups/{Uri.EscapeDataString(context.groupName)}?{commonQueryString}" };
        yield return new object[] { RestApiProvider.GetSendToConnectionEndpointAsync(context.appName, context.hubName, context.connectionId), $"{commonEndpoint}/connections/{Uri.EscapeDataString(context.connectionId)}/:send?{commonQueryString}" };
        yield return new object[] { RestApiProvider.GetConnectionGroupManagementEndpointAsync(context.appName, context.hubName, context.connectionId, context.groupName), $"{commonEndpoint}/groups/{Uri.EscapeDataString(context.groupName)}/connections/{Uri.EscapeDataString(context.connectionId)}?{commonQueryString}" };
    }
}
