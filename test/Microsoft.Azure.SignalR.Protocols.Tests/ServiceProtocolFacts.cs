// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.Azure.SignalR.Protocol.Tests
{
    public class ServiceProtocolFacts
    {
        private static readonly IServiceProtocol Protocol = new ServiceProtocol();

        public static IEnumerable<object[]> TestWriteData
        {
            get
            {
                foreach (var k in TestData.Keys)
                {
                    yield return new object[] { k };
                }
            }
        }

        public static IEnumerable<object[]> TestParseData
        {
            get
            {
                foreach (var k in TestData.Keys)
                {
                    yield return new object[] { k };
                }
            }
        }

        public static IEnumerable<object[]> TestParseOldData
        {
            get
            {
                foreach (var k in TestCompatibilityData.Keys)
                {
                    yield return new object[] { k };
                }
            }
        }
        public static IDictionary<string, ProtocolTestData> TestCompatibilityData => new[] {
            new ProtocolTestData(
                name: "CloseConnection",
                message: new CloseConnectionMessage("conn3"),
                binary: "kwWlY29ubjPA"),
            new ProtocolTestData(
                name: "CloseConnectionWithError",
                message: new CloseConnectionMessage("conn4", "Error message."),
                binary: "kwWlY29ubjSuRXJyb3IgbWVzc2FnZS4="),
            new ProtocolTestData(
                name: "CloseConnectionWithHeaders",
                message: new CloseConnectionMessage("conn4", "Error message.", new Dictionary<string, StringValues>() {
                    { "foo", "bar" }
                }),
                binary: "lAWlY29ubjSuRXJyb3IgbWVzc2FnZS6Bo2Zvb5GjYmFy"),
            new ProtocolTestData(
                name: "HandshakeRequest",
                message: new HandshakeRequestMessage(1),
                binary: "kgEB"),
            new ProtocolTestData(
                name: "HandshakeRequestWithProperty",
                message: new HandshakeRequestMessage(1) { ConnectionType = 1, Target = "abc" },
                binary: "lAEBAaNhYmM="),
        }.ToDictionary(t => t.Name);

        public static IDictionary<string, ProtocolTestData> TestData => new[]
        {
            new ProtocolTestData(
                name: "HandshakeRequest",
                message: new HandshakeRequestMessage(1),
                binary: "lQEBAKAA"),
            new ProtocolTestData(
                name: "HandshakeRequestWithProperty",
                message: new HandshakeRequestMessage(1) { ConnectionType = 1, Target = "abc" },
                binary: "lQEBAaNhYmMA"),
            new ProtocolTestData(
                name: "HandshakeRequestWithMigratableStatus",
                message: new HandshakeRequestMessage(1) { MigrationLevel = 1},
                binary: "lQEBAKAB"),
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
                name: "Ping+",
                message: new PingMessage { Messages = new string[] { "a", "b" } },
                binary: "kwOhYaFi"),
            new ProtocolTestData(
                name: "OpenConnection",
                message: new OpenConnectionMessage("conn1", null),
                binary: "lQSlY29ubjGAgKA="),
            new ProtocolTestData(
                name: "OpenConnectionWithClaims",
                message: new OpenConnectionMessage("conn2", new [] {new Claim(ClaimTypes.NameIdentifier, "user1")}),
                binary: "lQSlY29ubjKB2URodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllcqV1c2VyMYCg"),
            new ProtocolTestData(
                name: "OpenConnectionWithHeaders",
                message: new OpenConnectionMessage("conn3", null, new Dictionary<string, StringValues>
                {
                    {"header-key-1", "header-value-1"},
                    {"header-key-2", new[] {"heaer-value-2a", "header-value-2b"}},
                    {"header-key-3", new[] {"heaer-value-3a", "header-value-3b", "header-value-3c"}}
                }, string.Empty),
                binary: "lQSlY29ubjOAg6xoZWFkZXIta2V5LTGRrmhlYWRlci12YWx1ZS0xrGhlYWRlci1rZXktMpKuaGVhZXItdmFsdWUtMmGvaGVhZGVyLXZhbHVlLTJirGhlYWRlci1rZXktM5OuaGVhZXItdmFsdWUtM2GvaGVhZGVyLXZhbHVlLTNir2hlYWRlci12YWx1ZS0zY6A="),
            new ProtocolTestData(
                name: "OpenConnectionWithQueryString1",
                message: new OpenConnectionMessage("conn4", null, new Dictionary<string, StringValues>(), "query1=value1"),
                binary: "lQSlY29ubjSAgK1xdWVyeTE9dmFsdWUx"),
            new ProtocolTestData(
                name: "OpenConnectionWithQueryString2",
                message: new OpenConnectionMessage("conn4", null, new Dictionary<string, StringValues>(), "query1=value1&query2=query2&query3=value3"),
                binary: "lQSlY29ubjSAgNkpcXVlcnkxPXZhbHVlMSZxdWVyeTI9cXVlcnkyJnF1ZXJ5Mz12YWx1ZTM="),
            new ProtocolTestData(
                name: "CloseConnection",
                message: new CloseConnectionMessage("conn3"),
                binary: "lAWlY29ubjOggA=="),
            new ProtocolTestData(
                name: "CloseConnectionWithError",
                message: new CloseConnectionMessage("conn4", "Error message."),
                binary: "lAWlY29ubjSuRXJyb3IgbWVzc2FnZS6A"),
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
                }, "59f9f481-4a18-4ef5-ab4e-8d3ad372062a"),
                binary: "kwrZJDU5ZjlmNDgxLTRhMTgtNGVmNS1hYjRlLThkM2FkMzcyMDYyYZCCpGpzb27EBwQFBgcBAgOrbWVzc2FnZXBhY2vEBwUGBwECAwQ="),
            new ProtocolTestData(
                name: "BroadcastExcept",
                message: new BroadcastDataMessage(new[] {"conn7", "conn8", "conn9"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }, Guid.NewGuid().ToString()),
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
                name: "UserJoinGroup",
                message: new UserJoinGroupMessage("conn10", "group1"),
                binary: "kxCmY29ubjEwpmdyb3VwMQ=="),
            new ProtocolTestData(
                name: "UserLeaveGroup",
                message: new UserLeaveGroupMessage("conn11", "group2"),
                binary: "kxGmY29ubjExpmdyb3VwMg=="),
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
            new ProtocolTestData(
                name: "ServiceError",
                message: new ServiceErrorMessage("Maximum message count limit reached: 100000"),
                binary: "kg/ZK01heGltdW0gbWVzc2FnZSBjb3VudCBsaW1pdCByZWFjaGVkOiAxMDAwMDA="),
            new ProtocolTestData(
                name: "JoinGroupWithAck",
                message: new JoinGroupWithAckMessage("conn14", "group1", 1), 
                binary: "lBKmY29ubjE0pmdyb3VwMQE="),
            new ProtocolTestData(
                name: "LeaveGroupWithAck",
                message: new LeaveGroupWithAckMessage("conn15", "group2", 1), 
                binary: "lBOmY29ubjE1pmdyb3VwMgE="),
            new ProtocolTestData(
                name: "Ack",
                message: new AckMessage(1, 100), 
                binary: "lBQBZKA="),
            new ProtocolTestData(
                name: "AckWithMessage",
                message: new AckMessage(2, 101, "Joined group successfully"),
                binary: "lBQCZblKb2luZWQgZ3JvdXAgc3VjY2Vzc2Z1bGx5"),
        }.ToDictionary(t => t.Name);

        [Theory]
        [MemberData(nameof(TestParseOldData))]
        public void ParseOldMessages(string testDataName)
        {
            var testData = TestCompatibilityData[testDataName];

            // Verify that the input binary string decodes to the expected MsgPack primitives
            var bytes = Convert.FromBase64String(testData.Binary);

            // Parse the input fully now.
            bytes = Frame(bytes);
            var message = ParseServiceMessage(bytes);
            Assert.Equal(testData.Message, message, ServiceMessageEqualityComparer.Instance);
        }

        [Theory]
        [MemberData(nameof(TestParseData))]
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
        [MemberData(nameof(TestWriteData))]
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
            // Legacy protocol
            var expectedMessage = new OpenConnectionMessage("id", null);
            var bytes = new byte[]
            {
                ArrayBytes(3),                          // Array Length
                4,                                      // Message Type: OpenConnectionMessage
                StringBytes(2), (byte)'i', (byte)'d',   // OpenConnectionMessage.ConnectionId
                MapBytes(0),                            // OpenConnectionMessage.Claims
                StringBytes(2), (byte)'e', (byte)'x'    // Extra trailing data
            };

            bytes = Frame(bytes);
            var message = ParseServiceMessage(bytes);
            var openConnectionMessage = Assert.IsType<OpenConnectionMessage>(message);
            Assert.Equal(expectedMessage, openConnectionMessage, ServiceMessageEqualityComparer.Instance);

            // Current protocol
            expectedMessage = new OpenConnectionMessage("id", null, new Dictionary<string, StringValues>(), "?k=v");
            bytes = new byte[]
            {
                ArrayBytes(5),                                              // Array Length
                4,                                                          // Message Type: OpenConnectionMessage
                StringBytes(2), (byte)'i', (byte)'d',                       // OpenConnectionMessage.ConnectionId
                MapBytes(0),                                                // OpenConnectionMessage.Claims
                MapBytes(0),                                                // OpenConnectionMessage.Headers
                StringBytes(4), (byte)'?', (byte)'k', (byte)'=', (byte)'v', // OpenConnectionMessage.QueryString
                StringBytes(2), (byte)'e', (byte)'x'                        // Extra trailing data
            };

            bytes = Frame(bytes);
            message = ParseServiceMessage(bytes);
            openConnectionMessage = Assert.IsType<OpenConnectionMessage>(message);
            Assert.Equal(expectedMessage, openConnectionMessage, ServiceMessageEqualityComparer.Instance);
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

        public class ProtocolTestData
        {
            public string Name { get; private set; }
            public string Binary { get; private set; }
            public ServiceMessage Message { get; private set; }

            public ProtocolTestData(string name, ServiceMessage message, string binary)
            {
                Name = name;
                Message = message;
                Binary = binary;
            }
        }
    }
}
