// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management;

internal class RestApiProvider
{
    private const string Version = "2022-06-01";
    public const string HealthApiPath = $"api/health?api-version={Version}";

    private readonly string _serverEndpoint;

    public RestApiProvider(ServiceEndpoint endpoint)
    {
        _serverEndpoint = endpoint.ServerEndpoint.AbsoluteUri;
    }

    public RestApiEndpoint GetServiceHealthEndpoint()
    {
        var url = $"{_serverEndpoint}api/health?api-version={Version}";
        return new RestApiEndpoint(url);
    }

    public RestApiEndpoint GetBroadcastEndpoint(string appName, string hubName, IReadOnlyList<string> excluded = null)
    {
        var queries = excluded == null ? null : new Dictionary<string, StringValues>() { { "excluded", excluded.ToArray() } };
        return GenerateRestApiEndpoint(appName, hubName, "/:send", queries);
    }

    public RestApiEndpoint GetUserGroupManagementEndpoint(string appName, string hubName, string userId, string groupName)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/users/{Uri.EscapeDataString(userId)}/groups/{Uri.EscapeDataString(groupName)}");
    }

    public RestApiEndpoint GetSendToUserEndpoint(string appName, string hubName, string userId)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/users/{Uri.EscapeDataString(userId)}/:send");
    }

    public RestApiEndpoint GetSendToGroupEndpoint(string appName, string hubName, string groupName, IReadOnlyList<string> excluded = null)
    {
        var queries = excluded == null ? null : new Dictionary<string, StringValues>() { { "excluded", excluded.ToArray() } };
        return GenerateRestApiEndpoint(appName, hubName, $"/groups/{Uri.EscapeDataString(groupName)}/:send", queries);
    }

    public RestApiEndpoint GetRemoveUserFromAllGroupsEndpoint(string appName, string hubName, string userId)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/users/{Uri.EscapeDataString(userId)}/groups");
    }

    public RestApiEndpoint GetRemoveConnectionFromAllGroupsEndpoint(string appName, string hubName, string connectionId)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/connections/{Uri.EscapeDataString(connectionId)}/groups");
    }

    public RestApiEndpoint GetSendToConnectionEndpoint(string appName, string hubName, string connectionId)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/connections/{Uri.EscapeDataString(connectionId)}/:send");
    }

    public RestApiEndpoint GetConnectionGroupManagementEndpoint(string appName, string hubName, string connectionId, string groupName)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/groups/{Uri.EscapeDataString(groupName)}/connections/{Uri.EscapeDataString(connectionId)}");
    }

    public RestApiEndpoint GetCloseConnectionEndpoint(string appName, string hubName, string connectionId, string reason)
    {
        var queries = reason == null ? null : new Dictionary<string, StringValues>() { { "reason", reason } };
        return GenerateRestApiEndpoint(appName, hubName, $"/connections/{Uri.EscapeDataString(connectionId)}", queries: queries);
    }

    public RestApiEndpoint GetCheckConnectionExistsEndpoint(string appName, string hubName, string connectionId)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/connections/{Uri.EscapeDataString(connectionId)}");
    }

    public RestApiEndpoint GetCheckUserExistsEndpoint(string appName, string hubName, string user)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/users/{Uri.EscapeDataString(user)}");
    }

    public RestApiEndpoint GetCheckGroupExistsEndpoint(string appName, string hubName, string group)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/groups/{Uri.EscapeDataString(group)}");
    }

    public RestApiEndpoint GetSendStreamItemEndpoint(string appName, string hubName, string connectionId, string streamId)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/connections/{Uri.EscapeDataString(connectionId)}/streams/{Uri.EscapeDataString(streamId)}/:send");
    }

    public RestApiEndpoint GetSendStreamCompletionEndpoint(string appName, string hubName, string connectionId, string streamId)
    {
        return GenerateRestApiEndpoint(appName, hubName, $"/connections/{Uri.EscapeDataString(connectionId)}/streams/{Uri.EscapeDataString(streamId)}/:complete");
    }

    private RestApiEndpoint GenerateRestApiEndpoint(string appName, string hubName, string pathAfterHub, IDictionary<string, StringValues> queries = null)
    {
        var requestPrefixWithHub = $"{_serverEndpoint}api/hubs/{Uri.EscapeDataString(hubName.ToLowerInvariant())}";
        pathAfterHub = string.IsNullOrEmpty(appName)
            ? $"{pathAfterHub}?api-version={Version}"
            : $"{pathAfterHub}?application={Uri.EscapeDataString(appName.ToLowerInvariant())}&api-version={Version}";
        return new RestApiEndpoint($"{requestPrefixWithHub}{pathAfterHub}") { Query = queries };
    }
}
