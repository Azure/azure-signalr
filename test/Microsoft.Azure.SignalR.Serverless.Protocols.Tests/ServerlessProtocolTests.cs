// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;

using Newtonsoft.Json;

using Xunit;

namespace Microsoft.Azure.SignalR.Serverless.Protocols.Tests;

public class ServerlessProtocolTests
{
    public static IEnumerable<object[]> GetParameters()
    {
        var protocols = new string[] { "json", "messagepack" };
        foreach (var protocol in protocols)
        {
            yield return new object[] { protocol, null, Guid.NewGuid().ToString(), Array.Empty<object>() };
            yield return new object[] { protocol, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Array.Empty<object>() };
            yield return new object[]
            {
                protocol,
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                new object[] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString()}
            };
            yield return new object[]
            {
                protocol,
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                new object[] {new object[] {Guid.NewGuid().ToString()}, Guid.NewGuid().ToString()}
            };
            yield return new object[]
            {
                protocol,
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), new object[] { new Dictionary<string, string>
                {
                    [Guid.NewGuid().ToString()] = Guid.NewGuid().ToString(),
                    [Guid.NewGuid().ToString()] = Guid.NewGuid().ToString(),
                }}
            };
            yield return new object[]
            {
                protocol,
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                new object[] {new object[] { null, Guid.NewGuid().ToString() }}
            };
            yield return new object[]
            {
                protocol,
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                new object[] { Array.Empty<byte>(), new byte[] { 1, 2, 3 } }
            };
            yield return new object[]
            {
                protocol,
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                new object[]
                {
                    new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["nested"] = new object[] { "value", new byte[] { 1, 2, 3 }, null },
                            ["empty"] = new Dictionary<string, object>()
                        },
                        Array.Empty<object>()
                    },
                    new Dictionary<string, object> { ["sibling"] = new object[] { 1, "value" } },
                    "last"
                }
            };
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvocationMessageRejectsNestedAllocationAmplification(bool nestedMap)
    {
        var bytes = new List<byte>
        {
            0x95, 0x01, 0x80, 0xc0, 0xa1, (byte)'t',
            0x91,
            0xdc, 0x00, 0x10
        };
        bytes.AddRange(nestedMap
            ? new byte[] { 0xde, 0x00, 0x08 }
            : new byte[] { 0xdc, 0x00, 0x10 });
        bytes.AddRange(new byte[16]);
        var payload = new ReadOnlySequence<byte>(bytes.ToArray());
        var protocol = new MessagePackServerlessProtocol();

        if (nestedMap)
        {
            Assert.Throws<EndOfStreamException>(() => protocol.TryParseMessage(ref payload, out _));
        }
        else
        {
            var exception = Assert.Throws<InvalidDataException>(() => protocol.TryParseMessage(ref payload, out _));
            Assert.IsType<EndOfStreamException>(exception.InnerException);
        }
    }

    [Theory]
    [MemberData(nameof(GetParameters))]
    public void InvocationMessageParseTest(string protocolName, string invocationId, string target, object[] arguments)
    {
        var message = new AspNetCore.SignalR.Protocol.InvocationMessage(invocationId, target, arguments);
        IHubProtocol protocol = protocolName == "json" ? (IHubProtocol)new JsonHubProtocol() : new MessagePackHubProtocol();
        var bytes = new ReadOnlySequence<byte>(protocol.GetMessageBytes(message));
        ReadOnlySequence<byte> payload;
        if (protocolName == "json")
        {
            TextMessageParser.TryParseMessage(ref bytes, out payload);
        }
        else
        {
            BinaryMessageParser.TryParseMessage(ref bytes, out payload);
        }
        var serverlessProtocol = protocolName == "json" ? (IServerlessProtocol)new JsonServerlessProtocol() : new MessagePackServerlessProtocol();
        Assert.True(serverlessProtocol.TryParseMessage(ref payload, out var parsedMessage));
        var invocationMessage = (InvocationMessage)parsedMessage;
        Assert.Equal(1, invocationMessage.Type);
        Assert.Equal(invocationId, invocationMessage.InvocationId);
        Assert.Equal(target, invocationMessage.Target);
        var expected = JsonConvert.SerializeObject(arguments);
        var actual = JsonConvert.SerializeObject(invocationMessage.Arguments);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OpenConnectionMessageParseTest()
    {
        var openConnectionPayload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new OpenConnectionMessage { Type = 10 })));
        var serverlessProtocol = new JsonServerlessProtocol();
        Assert.True(serverlessProtocol.TryParseMessage(ref openConnectionPayload, out var message));
        Assert.Equal(typeof(OpenConnectionMessage), message.GetType());
    }

    [Theory]
    [InlineData("")]
    [InlineData("error")]
    [InlineData(null)]
    public void CloseConnectionMessageParseTest(string error)
    {
        var openConnectionPayload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new CloseConnectionMessage() { Type = 11, Error = error })));
        var serverlessProtocol = new JsonServerlessProtocol();
        Assert.True(serverlessProtocol.TryParseMessage(ref openConnectionPayload, out var message));
        Assert.Equal(error, ((CloseConnectionMessage)message).Error);
        Assert.Equal(typeof(CloseConnectionMessage), message.GetType());
    }
}
