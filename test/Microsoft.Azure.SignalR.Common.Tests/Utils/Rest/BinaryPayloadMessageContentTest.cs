// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.AspNetCore.SignalR.Protocol;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class BinaryPayloadMessageContentTest
    {
        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method", Justification = "<Pending>")]
        public void OneHubProtocolTest()
        {
            var payload = new PayloadMessage { Target = "target", Arguments = new object[] { "a", 1 } };
            var protocols = new List<IHubProtocol>() { new MessagePackHubProtocol() };
            using var httpContent = new BinaryPayloadMessageContent(payload, protocols);
            var actualBytes = new MemoryStream();
            httpContent.CopyToAsync(actualBytes, null, default).GetAwaiter().GetResult();
            var expectedBytes = new ArrayBufferWriter<byte>();
            var messagePackWriter = new MessagePackWriter(expectedBytes);
            messagePackWriter.WriteMapHeader(1);
            messagePackWriter.WriteString(Encoding.UTF8.GetBytes(Constants.Protocol.MessagePack));
            messagePackWriter.Write(protocols[0].GetMessageBytes(new InvocationMessage(payload.Target, payload.Arguments)).Span);
            messagePackWriter.Flush();
            Assert.True(expectedBytes.WrittenSpan.SequenceEqual(actualBytes.ToArray()));
        }

        [Fact]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "xUnit1031:Do not use blocking task operations in test method", Justification = "<Pending>")]
        public void TwoHubProtocolTest()
        {
            var payload = new PayloadMessage { Target = "target", Arguments = new object[] { "a", 1 } };
            var protocols = new List<IHubProtocol>() { new MessagePackHubProtocol(), new JsonHubProtocol() };
            using var httpContent = new BinaryPayloadMessageContent(payload, protocols);
            var actualBytes = new MemoryStream();
            httpContent.CopyToAsync(actualBytes, null, default).GetAwaiter().GetResult();
            var expectedBytes = new ArrayBufferWriter<byte>();
            var messagePackWriter = new MessagePackWriter(expectedBytes);
            messagePackWriter.WriteMapHeader(2);
            messagePackWriter.WriteString(Encoding.UTF8.GetBytes(Constants.Protocol.MessagePack));
            messagePackWriter.Write(protocols[0].GetMessageBytes(new InvocationMessage(payload.Target, payload.Arguments)).Span);
            messagePackWriter.WriteString(Encoding.UTF8.GetBytes(Constants.Protocol.Json));
            messagePackWriter.Write(protocols[1].GetMessageBytes(new InvocationMessage(payload.Target, payload.Arguments)).Span);
            messagePackWriter.Flush();
            Assert.True(expectedBytes.WrittenSpan.SequenceEqual(actualBytes.ToArray()));
        }
    }
}
