// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.SignalR;

#nullable enable

// TODO: make public later
internal sealed record SignalRGroupConnection
{
    [JsonPropertyName("connectionId")]
    public string ConnectionId { internal set; get; }

    [JsonPropertyName("userId")]
    public string? UserId { get; internal set; }

    public SignalRGroupConnection(string connectionId, string? userId = default)
    {
        ConnectionId = connectionId;
        UserId = userId;
    }
}
