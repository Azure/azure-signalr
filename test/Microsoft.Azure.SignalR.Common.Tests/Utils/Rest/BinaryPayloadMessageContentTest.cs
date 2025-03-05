// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using MessagePack;

using Microsoft.AspNetCore.SignalR.Protocol;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests;

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
  
    [Theory]
    [MemberData(nameof(GetTestData))]
    public async Task TestSerialization(HubMessage message, IHubProtocol[] protocols, ReadOnlyMemory<byte> expectedBytes)
    {
        using var httpContent = new BinaryPayloadMessageContent(message, protocols);
        var actualBytes = new MemoryStream();
        await httpContent.CopyToAsync(actualBytes, null, default);
        Assert.True(expectedBytes.Span.SequenceEqual(actualBytes.ToArray()));
    }

    public static IEnumerable<object[]> GetTestData() =>
        from message in GetMessages()
        from protocols in GetProtocols()
        select new object[] { message, protocols, GetExpectedBytes(message, protocols) };

    private static IEnumerable<HubMessage> GetMessages()
    {
        yield return new InvocationMessage("target", new object[] { "a", 1 });
        yield return new InvocationMessage("target", new object[] { });
        yield return new StreamItemMessage("id", null);
        yield return new StreamItemMessage("id", true);
        yield return new StreamItemMessage("id", 1);
        yield return new StreamItemMessage("id", new { a = 1 });
        yield return new StreamItemMessage("id", new object[] { "a", 1 });
    }

    private static IEnumerable<IHubProtocol[]> GetProtocols()
    {
        yield return new IHubProtocol[] { new JsonHubProtocol() };
        yield return new IHubProtocol[] { new MessagePackHubProtocol() };
        yield return new IHubProtocol[] { new MessagePackHubProtocol(), new JsonHubProtocol() };
    }

    private static ReadOnlyMemory<byte> GetExpectedBytes(HubMessage message, IHubProtocol[] protocols)
    {
        var expectedBytes = new ArrayBufferWriter<byte>();
        var messagePackWriter = new MessagePackWriter(expectedBytes);
        messagePackWriter.WriteMapHeader(protocols.Length);
        foreach (var hubProtocol in protocols)
        {
            messagePackWriter.WriteString(Encoding.UTF8.GetBytes(hubProtocol.Name));
            messagePackWriter.Write(hubProtocol.GetMessageBytes(message).Span);
        }
        messagePackWriter.Flush();
        return expectedBytes.WrittenMemory;
    }
}
