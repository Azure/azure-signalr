// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

using Azure.Core.Serialization;

using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

internal class JsonPayloadMessageContent : HttpContent
{
    private static readonly MediaTypeHeaderValue ContentType = new("application/json")
    {
        CharSet = "utf-8"
    };
    private static readonly JsonWriterOptions JsonWriterOptions = new()
    {
        // We must skip validation because what we break the writing midway and write JSON in other ways.
        SkipValidation = true
    };
    private readonly HubMessage _payloadMessage;
    private readonly ObjectSerializer _jsonObjectSerializer;
    private readonly Type _typeHint;

    public JsonPayloadMessageContent(HubMessage payloadMessage, ObjectSerializer jsonObjectSerializer, Type typeHint)
    {
        _payloadMessage = payloadMessage ?? throw new System.ArgumentNullException(nameof(payloadMessage));
        _jsonObjectSerializer = jsonObjectSerializer;
        _typeHint = typeHint;
        Headers.ContentType = ContentType;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
    {
        if (_payloadMessage is InvocationMessage invocationMessage)
        {
            using var jsonWriter = new Utf8JsonWriter(stream, JsonWriterOptions);
            jsonWriter.WriteStartObject();
            jsonWriter.WriteString(nameof(PayloadMessage.Target), invocationMessage.Target);
            jsonWriter.WritePropertyName(nameof(PayloadMessage.Arguments));
            await jsonWriter.FlushAsync();
            await _jsonObjectSerializer.SerializeAsync(stream, invocationMessage.Arguments, typeof(object[]), default);
            jsonWriter.WriteEndObject();
            await jsonWriter.FlushAsync();
        }
        else if (_payloadMessage is StreamItemMessage streamItemMessage)
        {
            await _jsonObjectSerializer.SerializeAsync(stream, streamItemMessage.Item, _typeHint, default);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
