// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Core.Serialization;

namespace Microsoft.Azure.SignalR
{
    internal class PayloadMessageContent : HttpContent
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
        private readonly PayloadMessage _payloadMessage;
        private readonly ObjectSerializer _jsonObjectSerializer;

        public PayloadMessageContent(PayloadMessage payloadMessage, ObjectSerializer jsonObjectSerializer)
        {
            _payloadMessage = payloadMessage ?? throw new System.ArgumentNullException(nameof(payloadMessage));
            _jsonObjectSerializer = jsonObjectSerializer;
            Headers.ContentType = ContentType;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            using var jsonWriter = new Utf8JsonWriter(stream, JsonWriterOptions);
            jsonWriter.WriteStartObject();
            jsonWriter.WriteString(nameof(PayloadMessage.Target), _payloadMessage.Target);
            jsonWriter.WritePropertyName(nameof(PayloadMessage.Arguments));
            await jsonWriter.FlushAsync();
            await _jsonObjectSerializer.SerializeAsync(stream, _payloadMessage.Arguments, typeof(object[]), default);
            jsonWriter.WriteEndObject();
            await jsonWriter.FlushAsync();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
