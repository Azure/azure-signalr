// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Common
{
    internal class BinaryPayloadMessageContent : HttpContent
    {
        private static readonly Dictionary<string, byte[]> ProtocolMap = new Dictionary<string, byte[]>(2)
        {
            {Constants.Protocol.Json, Encoding.UTF8.GetBytes(Constants.Protocol.Json) },
            {Constants.Protocol.MessagePack,Encoding.UTF8.GetBytes(Constants.Protocol.MessagePack)}
        };
        private static readonly MediaTypeHeaderValue ContentType = new("application/octet-stream");

        private readonly PayloadMessage _payloadMessage;
        private readonly IReadOnlyList<IHubProtocol> _hubProtocols;

        public BinaryPayloadMessageContent(PayloadMessage payloadMessage, IReadOnlyList<IHubProtocol> hubProtocols)
        {
            _payloadMessage = payloadMessage ?? throw new ArgumentNullException(nameof(payloadMessage));
            _hubProtocols = hubProtocols ?? throw new ArgumentNullException(nameof(hubProtocols));
            Headers.ContentType = ContentType;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            var memoryBufferWriter = new MemoryBufferWriter();
            WriteMessageCore(memoryBufferWriter);
            await memoryBufferWriter.CopyToAsync(stream);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        private void WriteMessageCore(IBufferWriter<byte> bufferWriter)
        {
            var invocationMessage = new InvocationMessage(_payloadMessage.Target, _payloadMessage.Arguments);
            var messagePackWriter = new MessagePackWriter(bufferWriter);
            messagePackWriter.WriteMapHeader(_hubProtocols.Count);
            foreach (var hubProtocol in _hubProtocols)
            {
                messagePackWriter.WriteString(ProtocolMap[hubProtocol.Name]);
                messagePackWriter.Write(hubProtocol.GetMessageBytes(invocationMessage).Span);
            }
            messagePackWriter.Flush();
        }
    }
}
