// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.SignalR.Management.ClientInvocation;

sealed class InvocationResponse
{
    [JsonPropertyName("result")]
    public string Result { get; set; }

    [JsonPropertyName("protocol")]
    public string protocol { get; set; }
}
