// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MessagePack;

using Microsoft.AspNetCore.SignalR.Protocol;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests;

public class BinaryPayloadMessageContentTest
{
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
        yield return new InvocationMessage("target", ["a", 1]);
        yield return new InvocationMessage("target", []);
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
