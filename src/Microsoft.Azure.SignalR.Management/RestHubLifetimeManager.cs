// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Azure;

using Microsoft.AspNetCore.SignalR;
#if NET7_0_OR_GREATER
using Microsoft.AspNetCore.SignalR.Protocol;
#endif
using Microsoft.Extensions.Primitives;

using static Microsoft.Azure.SignalR.Constants;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

internal class RestHubLifetimeManager<THub> : HubLifetimeManager<THub>, IServiceHubLifetimeManager<THub> where THub : Hub
{
    private const string NullOrEmptyStringErrorMessage = "Argument cannot be null or empty.";
    private const string TtlOutOfRangeErrorMessage = "Ttl cannot be less than 0.";

    private readonly RestClient _restClient;
    private readonly RestApiProvider _restApiProvider;
    private readonly string _hubName;
    private readonly string _appName;
    private readonly IHubProtocolResolver _protocolResolver;

    public RestHubLifetimeManager(string hubName, ServiceEndpoint endpoint, string appName, RestClient restClient, IHubProtocolResolver protocolResolver)
    {
        _restApiProvider = new RestApiProvider(endpoint);
        _appName = appName;
        _hubName = hubName;
        _restClient = restClient;
        _protocolResolver = protocolResolver;
    }

    public override async Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }

        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
        }

        var api = _restApiProvider.GetConnectionGroupManagementEndpoint(_appName, _hubName, connectionId, groupName);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Put, handleExpectedResponse: static response => FilterExpectedResponse(response, ErrorCodes.ErrorConnectionNotExisted), cancellationToken: cancellationToken);
    }

    public override Task OnConnectedAsync(HubConnectionContext connection)
    {
        throw new NotSupportedException();
    }

    public override Task OnDisconnectedAsync(HubConnectionContext connection)
    {
        throw new NotSupportedException();
    }

    public override async Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }

        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
        }

        var api = _restApiProvider.GetConnectionGroupManagementEndpoint(_appName, _hubName, connectionId, groupName);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Delete, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

    public async Task RemoveFromAllGroupsAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }

        var api = _restApiProvider.GetRemoveConnectionFromAllGroupsEndpoint(_appName, _hubName, connectionId);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Delete, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

    public override Task SendAllAsync(string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        return SendAllExceptAsync(methodName, args, null, cancellationToken);
    }

    public override async Task SendAllExceptAsync(string methodName, object?[] args, IReadOnlyList<string>? excludedConnectionIds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(methodName))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
        }

        var api = _restApiProvider.GetBroadcastEndpoint(_appName, _hubName, excluded: excludedConnectionIds);
        await _restClient.SendMessageWithRetryAsync(api, HttpMethod.Post, methodName, args, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

    public override async Task SendConnectionAsync(string connectionId, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(methodName))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
        }

        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }

        var api = _restApiProvider.GetSendToConnectionEndpoint(_appName, _hubName, connectionId);
        await _restClient.SendMessageWithRetryAsync(api, HttpMethod.Post, methodName, args, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

    public override async Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(connectionIds.Select(id => SendConnectionAsync(id, methodName, args, cancellationToken)));
    }

    public override Task SendGroupAsync(string groupName, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        return SendGroupExceptAsync(groupName, methodName, args, null, cancellationToken);
    }

    public override async Task SendGroupExceptAsync(string groupName, string methodName, object?[] args, IReadOnlyList<string>? excludedConnectionIds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(methodName))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
        }

        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
        }

        var api = _restApiProvider.GetSendToGroupEndpoint(_appName, _hubName, groupName, excluded: excludedConnectionIds);
        await _restClient.SendMessageWithRetryAsync(api, HttpMethod.Post, methodName, args, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

    public override async Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        Task? all = null;

        try
        {
            await (all = Task.WhenAll(from groupName in groupNames
                                      select SendGroupAsync(groupName, methodName, args, cancellationToken)));
        }
        catch
        {
            throw all!.Exception!;
        }
    }

    public override async Task SendUserAsync(string userId, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(methodName))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
        }

        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
        }

        var api = _restApiProvider.GetSendToUserEndpoint(_appName, _hubName, userId);
        await _restClient.SendMessageWithRetryAsync(api, HttpMethod.Post, methodName, args, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

    public override async Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        Task? all = null;

        try
        {
            await (all = Task.WhenAll(from userId in userIds
                                      select SendUserAsync(userId, methodName, args, cancellationToken)));
        }
        catch
        {
            throw all!.Exception!;
        }
    }

    public async Task UserAddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
    {
        ValidateUserIdAndGroupName(userId, groupName);

        var api = _restApiProvider.GetUserGroupManagementEndpoint(_appName, _hubName, userId, groupName);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Put, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

    public async Task UserAddToGroupAsync(string userId, string groupName, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        ValidateUserIdAndGroupName(userId, groupName);

        if (ttl < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), TtlOutOfRangeErrorMessage);
        }
        var api = _restApiProvider.GetUserGroupManagementEndpoint(_appName, _hubName, userId, groupName);
        api.Query = new Dictionary<string, StringValues>
        {
            ["ttl"] = ((int)ttl.TotalSeconds).ToString(CultureInfo.InvariantCulture),
        };
        await _restClient.SendWithRetryAsync(api, HttpMethod.Put, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

    public async Task UserRemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default)
    {
        ValidateUserIdAndGroupName(userId, groupName);

        var api = _restApiProvider.GetUserGroupManagementEndpoint(_appName, _hubName, userId, groupName);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Delete, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

    public async Task UserRemoveFromAllGroupsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var api = _restApiProvider.GetRemoveUserFromAllGroupsEndpoint(_appName, _hubName, userId);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Delete, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

    public async Task<bool> IsUserInGroup(string userId, string groupName, CancellationToken cancellationToken = default)
    {
        var isUserInGroup = false;
        var api = _restApiProvider.GetUserGroupManagementEndpoint(_appName, _hubName, userId, groupName);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Get, handleExpectedResponse: response =>
            {
                isUserInGroup = response.StatusCode == HttpStatusCode.OK;
                return FilterExpectedResponse(response, ErrorCodes.InfoUserNotInGroup);
            }, cancellationToken: cancellationToken);
        return isUserInGroup;
    }

    public async Task CloseConnectionAsync(string connectionId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }
        var api = _restApiProvider.GetCloseConnectionEndpoint(_appName, _hubName, connectionId, reason);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Delete, handleExpectedResponse: static response => FilterExpectedResponse(response, ErrorCodes.WarningConnectionNotExisted), cancellationToken: cancellationToken);
    }

    private static void ValidateUserIdAndGroupName(string userId, string groupName)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
        }

        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
        }
    }

    public async Task<bool> ConnectionExistsAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }
        var exists = false;
        var api = _restApiProvider.GetCheckConnectionExistsEndpoint(_appName, _hubName, connectionId);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Head, handleExpectedResponse: response =>
        {
            exists = response.StatusCode == HttpStatusCode.OK;
            return FilterExpectedResponse(response, ErrorCodes.WarningConnectionNotExisted);
        }, cancellationToken: cancellationToken);
        return exists;
    }

    public async Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(userId));
        }
        var exists = false;
        var api = _restApiProvider.GetCheckUserExistsEndpoint(_appName, _hubName, userId);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Head, handleExpectedResponse: response =>
        {
            exists = response.StatusCode == HttpStatusCode.OK;
            return FilterExpectedResponse(response, ErrorCodes.WarningUserNotExisted);
        }, cancellationToken: cancellationToken);
        return exists;
    }

    public async Task<bool> GroupExistsAsync(string groupName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(groupName))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(groupName));
        }
        var exists = false;
        var api = _restApiProvider.GetCheckGroupExistsEndpoint(_appName, _hubName, groupName);
        await _restClient.SendWithRetryAsync(api, HttpMethod.Head, handleExpectedResponse: response =>
        {
            exists = response.StatusCode == HttpStatusCode.OK;
            return FilterExpectedResponse(response, ErrorCodes.WarningGroupNotExisted);
        }, cancellationToken: cancellationToken);
        return exists;
    }

    public async Task SendStreamItemAsync<TItem>(string connectionId, string streamId, TItem item, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }
        if (string.IsNullOrEmpty(streamId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(streamId));
        }
        var api = _restApiProvider.GetSendStreamItemEndpoint(_appName, _hubName, connectionId, streamId);
        await _restClient.SendStreamMessageWithRetryAsync(api, HttpMethod.Post, streamId, item, typeof(TItem), cancellationToken: cancellationToken);
    }

    public async Task SendStreamCompletionAsync(string connectionId, string streamId, string error, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
        }
        if (string.IsNullOrEmpty(streamId))
        {
            throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(streamId));
        }
        var api = _restApiProvider.GetSendStreamCompletionEndpoint(_appName, _hubName, connectionId, streamId);
        if (!string.IsNullOrEmpty(error))
        {
            api.Query = new Dictionary<string, StringValues>
            {
                ["error"] = error,
            };
        }
        await _restClient.SendWithRetryAsync(api, HttpMethod.Post, handleExpectedResponse: null, cancellationToken: cancellationToken);
    }

#if NET7_0_OR_GREATER

#pragma warning disable IDE0051 // Will be used in the future updates
    private static bool IsInvocationSupported(IHubProtocol protocol)
#pragma warning restore IDE0051 // Will be used in the future updates
    {
        // Use protocol.Name to check for supported protocols
        switch (protocol.Name)
        {
            case Constants.Protocol.Json:
            case Constants.Protocol.MessagePack:
                return true;
            default:
                return false;
        }
    }

#endif

    private static bool FilterExpectedResponse(HttpResponseMessage response, string expectedErrorCode) =>
        response.IsSuccessStatusCode
        || (response.StatusCode == HttpStatusCode.NotFound && response.Headers.TryGetValues(Headers.MicrosoftErrorCode, out var errorCodes) && errorCodes.First().Equals(expectedErrorCode, StringComparison.OrdinalIgnoreCase));

    public AsyncPageable<SignalRGroupConnection> ListConnectionsInGroup(string groupName, int? top = null, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(groupName))
        {
            throw new ArgumentException($"'{nameof(groupName)}' cannot be null or whitespace.", nameof(groupName));
        }

        if (top < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(top), "The value must be greater than or equal to 0.");
        }

        return new PageableGroupMember(FetchPages, token);

        async IAsyncEnumerable<Page<SignalRGroupConnection>> FetchPages(string? continuationToken, int? pageSizeHint)
        {
            // Calculate the api for the first page
            var api = _restApiProvider.GetListConnectionsInGroupEndpoint(_appName, _hubName, groupName);
            if (top.HasValue)
            {
                api.Query = new Dictionary<string, StringValues>
                {
                    ["top"] = top.Value.ToString(CultureInfo.InvariantCulture),
                };
            }
            if (pageSizeHint.HasValue)
            {
                api.Query ??= new Dictionary<string, StringValues>();
                api.Query["maxPageSize"] = pageSizeHint.Value.ToString(CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrEmpty(continuationToken))
            {
                api.Query ??= new Dictionary<string, StringValues>();
                api.Query["continuationToken"] = continuationToken;
            }
            do
            {
                var page = await FetchSinglePage(api, token);
                continuationToken = page.ContinuationToken;
                yield return page;
                if (page.ContinuationToken == null)
                {
                    yield break;
                }
                if (top != null)
                {
                    top -= page.Values.Count;
                    if (top <= 0)
                    {
                        yield break;
                    }
                }
                // Actually it's the next link
                api = new RestApiEndpoint(page.ContinuationToken);
            } while (true);
        }

        async Task<Page<SignalRGroupConnection>> FetchSinglePage(RestApiEndpoint api, CancellationToken cancellationToken = default)
        {
            var page = default(Page<SignalRGroupConnection>);

            await _restClient.SendWithRetryAsync(api, HttpMethod.Get, async response =>
            {
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }
                var contentStream = await response.Content.ReadAsStreamAsync();
                page = await JsonSerializer.DeserializeAsync<GroupMemberQueryResultPage>(contentStream, cancellationToken: token);
                return true;
            }, cancellationToken: token);
            return page!;
        }
    }
}
