// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.Azure.SignalR.Management.ClientInvocation;
#nullable enable
sealed class InvocationResponse<T>
{
    [JsonPropertyName("result")]
    public T? Result { get; set; }
}
