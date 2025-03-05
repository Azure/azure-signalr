// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;

using Azure.Core.Serialization;

using Microsoft.AspNetCore.SignalR.Protocol;

#nullable enable
namespace Microsoft.Azure.SignalR.Common;

internal class JsonPayloadContentBuilder : IPayloadContentBuilder
{
    private readonly ObjectSerializer _jsonObjectSerializer;

    public JsonPayloadContentBuilder(ObjectSerializer jsonObjectSerializer)
    {
        _jsonObjectSerializer = jsonObjectSerializer;
    }

    public HttpContent? Build(HubMessage? payload, Type? typeHint)
    {
        return payload == null ? null : new JsonPayloadMessageContent(payload, _jsonObjectSerializer, typeHint);
    }
}
