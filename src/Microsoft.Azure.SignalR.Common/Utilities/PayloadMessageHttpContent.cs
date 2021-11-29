// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Azure.Core.Serialization;

namespace Microsoft.Azure.SignalR
{
    internal class PayloadMessageContent : HttpContent
    {
        private readonly PayloadMessage _payloadMessage;
        private readonly ObjectSerializer _jsonObjectSerializer;

        public PayloadMessageContent(PayloadMessage payloadMessage, ObjectSerializer jsonObjectSerializer)
        {
            _payloadMessage = payloadMessage;
            _jsonObjectSerializer = jsonObjectSerializer;
            Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            var reusableUtf8JsonWriter = ReusableUtf8JsonWriter.Get(stream);
            var jsonWriter = reusableUtf8JsonWriter.GetJsonWriter();
            jsonWriter.WriteStartObject();
            jsonWriter.WriteString(nameof(PayloadMessage.Target), _payloadMessage.Target);
            jsonWriter.WritePropertyName(nameof(PayloadMessage.Arguments));
            await jsonWriter.FlushAsync();
            await _jsonObjectSerializer.SerializeAsync(stream, _payloadMessage.Arguments, typeof(object[]), default);
            jsonWriter.WriteEndObject();
            await jsonWriter.FlushAsync();
            ReusableUtf8JsonWriter.Return(reusableUtf8JsonWriter);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
