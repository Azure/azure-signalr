// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Xunit;

namespace Microsoft.Azure.SignalR.Protocol.Tests
{
    public class ServiceProtocolFacts
    {
        private static readonly IServiceProtocol Protocol = new ServiceProtocol();

        public static IEnumerable<object[]> TestDataNames
        {
            get
            {
                foreach (var k in TestData.Keys)
                {
                    yield return new object[] { k };
                }
            }
        }

        public static IDictionary<string, ProtocolTestData> TestData => new[]
        {
            new ProtocolTestData(
                name: "HandshakeRequest",
                message: new HandshakeRequestMessage(1),
                binary: "kgEB"),
            new ProtocolTestData(
                name: "HandshakeResponse",
                message: new HandshakeResponseMessage(),
                binary: "kgKg"),
            new ProtocolTestData(
                name: "HandshakeResponseWithError",
                message: new HandshakeResponseMessage("Version mismatch."),
                binary: "kgKxVmVyc2lvbiBtaXNtYXRjaC4="),
            new ProtocolTestData(
                name: "Ping",
                message: PingMessage.Instance,
                binary: "kQM="),
            new ProtocolTestData(
                name: "OpenConnection",
                message: new OpenConnectionMessage("conn1", null),
                binary: "kwSlY29ubjGA"),
            new ProtocolTestData(
                name: "OpenConnectionWithClaims",
                message: new OpenConnectionMessage("conn2", new [] {new Claim(ClaimTypes.NameIdentifier, "user1")}),
                binary: "kwSlY29ubjKB2URodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllcqV1c2VyMQ=="),
            new ProtocolTestData(
                name: "CloseConnection",
                message: new CloseConnectionMessage("conn3"),
                binary: "kwWlY29ubjPA"),
            new ProtocolTestData(
                name: "CloseConnectionWithError",
                message: new CloseConnectionMessage("conn4", "Error message."),
                binary: "kwWlY29ubjSuRXJyb3IgbWVzc2FnZS4="),
            new ProtocolTestData(
                name: "ConnectionData",
                message: new ConnectionDataMessage("conn5", new byte[] {1, 2, 3, 4, 5, 6, 7}),
                binary: "kwalY29ubjXEBwECAwQFBgc="),
            new ProtocolTestData(
                name: "MultiConnectionData",
                message: new MultiConnectionDataMessage(new [] {"conn6", "conn7"}, new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["json"] = new byte[] {2, 3, 4, 5, 6, 7, 1},
                    ["messagepack"] = new byte[] {3, 4, 5, 6, 7, 1, 2}
                }),
                binary: "kweSpWNvbm42pWNvbm43gqRqc29uxAcCAwQFBgcBq21lc3NhZ2VwYWNrxAcDBAUGBwEC"),
            new ProtocolTestData(
                name: "UserData",
                message: new UserDataMessage("user1",
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "kwildXNlcjGCpGpzb27EBwYHAQIDBAWrbWVzc2FnZXBhY2vEBwcBAgMEBQY="),
            new ProtocolTestData(
                name: "MultiUserData",
                message: new MultiUserDataMessage(new [] {"user1", "user2"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "kwmSpXVzZXIxpXVzZXIygqRqc29uxAcGBwECAwQFq21lc3NhZ2VwYWNrxAcHAQIDBAUG"),
            new ProtocolTestData(
                name: "Broadcast",
                message: new BroadcastDataMessage(new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["json"] = new byte[] {4, 5, 6, 7, 1, 2, 3},
                    ["messagepack"] = new byte[] {5, 6, 7, 1, 2, 3, 4}
                }),
                binary: "kwqQgqRqc29uxAcEBQYHAQIDq21lc3NhZ2VwYWNrxAcFBgcBAgME"),
            new ProtocolTestData(
                name: "BroadcastExcept",
                message: new BroadcastDataMessage(new[] {"conn7", "conn8", "conn9"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "kwqTpWNvbm43pWNvbm44pWNvbm45gqRqc29uxAcGBwECAwQFq21lc3NhZ2VwYWNrxAcHAQIDBAUG"),
            new ProtocolTestData(
                name: "JoinGroup",
                message: new JoinGroupMessage("conn10", "group1"),
                binary: "kwumY29ubjEwpmdyb3VwMQ=="),
            new ProtocolTestData(
                name: "LeaveGroup",
                message: new LeaveGroupMessage("conn11", "group2"),
                binary: "kwymY29ubjExpmdyb3VwMg=="),
            new ProtocolTestData(
                name: "GroupBroadcast",
                message: new GroupBroadcastDataMessage("group3",
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "lA2mZ3JvdXAzkIKkanNvbsQHBgcBAgMEBattZXNzYWdlcGFja8QHBwECAwQFBg=="),
            new ProtocolTestData(
                name: "GroupBroadcastExcept",
                message: new GroupBroadcastDataMessage("group3", new [] {"conn12", "conn13"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "lA2mZ3JvdXAzkqZjb25uMTKmY29ubjEzgqRqc29uxAcGBwECAwQFq21lc3NhZ2VwYWNrxAcHAQIDBAUG"),
            new ProtocolTestData(
                name: "MultiGroupBroadcast",
                message: new MultiGroupBroadcastDataMessage(new [] {"group4", "group5"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {1, 2, 3, 4, 5, 6, 7, 8},
                        ["messagepack"] = new byte[] {7, 8, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "kw6Spmdyb3VwNKZncm91cDWCpGpzb27ECAECAwQFBgcIq21lc3NhZ2VwYWNrxAgHCAECAwQFBg=="),
        }.ToDictionary(t => t.Name);

        [Theory]
        [MemberData(nameof(TestDataNames))]
        public void ParseMessages(string testDataName)
        {
            var testData = TestData[testDataName];

            // Verify that the input binary string decodes to the expected MsgPack primitives
            var bytes = Convert.FromBase64String(testData.Binary);

            // Parse the input fully now.
            bytes = Frame(bytes);
            var message = ParseServiceMessage(bytes);
            Assert.Equal(testData.Message, message, ServiceMessageEqualityComparer.Instance);
        }

        [Theory]
        [MemberData(nameof(TestDataNames))]
        public void WriteMessages(string testDataName)
        {
            var testData = TestData[testDataName];

            var bytes = Protocol.GetMessageBytes(testData.Message);

            // Unframe the message to check the binary encoding
            var byteSpan = new ReadOnlySequence<byte>(bytes);
            Assert.True(BinaryMessageParser.TryParseMessage(ref byteSpan, out var unframed));

            // Check the baseline binary encoding, use Assert.True in order to configure the error message
            var actual = Convert.ToBase64String(unframed.ToArray());
            Assert.True(string.Equals(actual, testData.Binary, StringComparison.Ordinal),
$@"Binary encoding changed from
    [{testData.Binary}]
to
    [{actual}]
Please verify the MsgPack output and update the baseline");
        }

        [Fact]
        public void ParseMessageWithExtraData()
        {
            var expectedMessage = new OpenConnectionMessage("id", null);

            // Verify that the input binary string decodes to the expected MsgPack primitives
            var bytes = new byte[] { ArrayBytes(3), 4, StringBytes(2), (byte)'i', (byte)'d', MapBytes(0), StringBytes(2), (byte)'e', (byte)'x' };

            // Parse the input fully now.
            bytes = Frame(bytes);
            var message = ParseServiceMessage<OpenConnectionMessage>(bytes);
            Assert.Equal(expectedMessage, message, ServiceMessageEqualityComparer.Instance);
        }

        private static byte ArrayBytes(int size)
        {
            return (byte) (0x90 | size);
        }

        private static byte StringBytes(int size)
        {
            return (byte) (0xa0 | size);
        }

        private static byte MapBytes(int size)
        {
            return (byte) (0x80 | size);
        }

        private static byte[] Frame(byte[] input)
        {
            var stream = MemoryBufferWriter.Get();
            try
            {
                BinaryMessageFormatter.WriteLengthPrefix(input.Length, stream);
                stream.Write(input);
                return stream.ToArray();
            }
            finally
            {
                MemoryBufferWriter.Return(stream);
            }
        }

        private static ServiceMessage ParseServiceMessage(byte[] bytes)
        {
            var data = new ReadOnlySequence<byte>(bytes);
            Assert.True(Protocol.TryParseMessage(ref data, out var message));
            return message;
        }

        private static T ParseServiceMessage<T>(byte[] bytes) where T : ServiceMessage
        {
            var data = new ReadOnlySequence<byte>(bytes);
            Assert.True(Protocol.TryParseMessage(ref data, out var message));
            return Assert.IsType<T>(message);
        }

        public class ProtocolTestData
        {
            public string Name { get; }
            public string Binary { get; }
            public ServiceMessage Message { get; }

            public ProtocolTestData(string name, ServiceMessage message, string binary)
            {
                Name = name;
                Message = message;
                Binary = binary;
            }

            public override string ToString() => Name;
        }
    }
}
