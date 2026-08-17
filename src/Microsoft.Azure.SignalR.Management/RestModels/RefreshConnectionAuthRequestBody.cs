// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text.Json.Serialization;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

internal sealed class RefreshConnectionAuthRequestBody
{
    [JsonPropertyName("connectionToken")]
    public string ConnectionToken { get; set; } = string.Empty;

    [JsonPropertyName("expireTime")]
    public DateTimeOffset ExpireTime { get; set; }

    [JsonPropertyName("claims")]
    public ClaimDto[]? Claims { get; set; }
}
