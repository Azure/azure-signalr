// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

internal sealed class ClaimsResponseBody
{
    [JsonPropertyName("claims")]
    public ClaimDto[]? Claims { get; set; }
}
