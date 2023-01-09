// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Http;
using Azure.Core.Serialization;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Common;

#nullable enable

internal class JsonPayloadContentBuilder : IPayloadContentBuilder
{
    private readonly ObjectSerializer _jsonObjectSerializer;

    public JsonPayloadContentBuilder(ObjectSerializer jsonObjectSerializer)
    {
        _jsonObjectSerializer = jsonObjectSerializer;
    }

    public HttpContent? Build(PayloadMessage? payload)
    {
        return payload == null ? null : new JsonPayloadMessageContent(payload, _jsonObjectSerializer);
    }
}