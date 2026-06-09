// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.SignalR;

#nullable enable

public sealed record SignalRGroupMember
{
    [JsonPropertyName("connectionId")]
    public string ConnectionId { internal set; get; }

    [JsonPropertyName("userId")]
    public string? UserId { get; internal set; }

    public SignalRGroupMember(string connectionId, string? userId = default)
    {
        ConnectionId = connectionId;
        UserId = userId;
    }
}
