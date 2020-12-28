// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;

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
            new ProtocolTestData(
                name: "HandshakeRequest_NoOptionalField",
                message: new HandshakeRequestMessage(1),
                binary: "lQEBAKAA"),
            new ProtocolTestData(
                name: "HandshakeRequestWithProperty_NoOptionalField",
                message: new HandshakeRequestMessage(1) { ConnectionType = 1, Target = "abc" },
                binary: "lQEBAaNhYmMA"),
            new ProtocolTestData(
                name: "HandshakeRequestWithMigratableStatus_NoOptionalField",
                message: new HandshakeRequestMessage(1) { MigrationLevel = 1},
                binary: "lQEBAKAB"),
            new ProtocolTestData(
                name: "HandshakeResponse_NoOptionalField",
                message: new HandshakeResponseMessage(),
                binary: "kgKg"),
            new ProtocolTestData(
                name: "HandshakeResponseWithError_NoOptionalField",
                message: new HandshakeResponseMessage("Version mismatch."),
                binary: "kgKxVmVyc2lvbiBtaXNtYXRjaC4="),
            new ProtocolTestData(
                name: "OpenConnection_NoOptionalField",
                message: new OpenConnectionMessage("conn1", null),
                binary: "lQSlY29ubjGAgKA="),
            new ProtocolTestData(
                name: "OpenConnectionWithClaims_NoOptionalField",
                message: new OpenConnectionMessage("conn2", new [] {new Claim(ClaimTypes.NameIdentifier, "user1")}),
                binary: "lQSlY29ubjKB2URodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllcqV1c2VyMYCg"),
            new ProtocolTestData(
                name: "OpenConnectionWithHeaders_NoOptionalField",
                message: new OpenConnectionMessage("conn3", null, new Dictionary<string, StringValues>
                {
                    {"header-key-1", "header-value-1"},
                    {"header-key-2", new[] {"heaer-value-2a", "header-value-2b"}},
                    {"header-key-3", new[] {"heaer-value-3a", "header-value-3b", "header-value-3c"}}
                }, string.Empty),
                binary: "lQSlY29ubjOAg6xoZWFkZXIta2V5LTGRrmhlYWRlci12YWx1ZS0xrGhlYWRlci1rZXktMpKuaGVhZXItdmFsdWUtMmGvaGVhZGVyLXZhbHVlLTJirGhlYWRlci1rZXktM5OuaGVhZXItdmFsdWUtM2GvaGVhZGVyLXZhbHVlLTNir2hlYWRlci12YWx1ZS0zY6A="),
            new ProtocolTestData(
                name: "OpenConnectionWithQueryString1_NoOptionalField",
                message: new OpenConnectionMessage("conn4", null, new Dictionary<string, StringValues>(), "query1=value1"),
                binary: "lQSlY29ubjSAgK1xdWVyeTE9dmFsdWUx"),
            new ProtocolTestData(
                name: "OpenConnectionWithQueryString2_NoOptionalField",
                message: new OpenConnectionMessage("conn4", null, new Dictionary<string, StringValues>(), "query1=value1&query2=query2&query3=value3"),
                binary: "lQSlY29ubjSAgNkpcXVlcnkxPXZhbHVlMSZxdWVyeTI9cXVlcnkyJnF1ZXJ5Mz12YWx1ZTM="),
            new ProtocolTestData(
                name: "CloseConnection_NoOptionalField",
                message: new CloseConnectionMessage("conn3"),
                binary: "lAWlY29ubjOggA=="),
            new ProtocolTestData(
                name: "CloseConnectionWithError_NoOptionalField",
                message: new CloseConnectionMessage("conn4", "Error message."),
                binary: "lAWlY29ubjSuRXJyb3IgbWVzc2FnZS6A"),
            new ProtocolTestData(
                name: "ConnectionData_NoOptionalField",
                message: new ConnectionDataMessage("conn5", new byte[] {1, 2, 3, 4, 5, 6, 7}),
                binary: "kwalY29ubjXEBwECAwQFBgc="),
            new ProtocolTestData(
                name: "MultiConnectionData_NoOptionalField",
                message: new MultiConnectionDataMessage(new [] {"conn6", "conn7"}, new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["json"] = new byte[] {2, 3, 4, 5, 6, 7, 1},
                    ["messagepack"] = new byte[] {3, 4, 5, 6, 7, 1, 2}
                }),
                binary: "kweSpWNvbm42pWNvbm43gqRqc29uxAcCAwQFBgcBq21lc3NhZ2VwYWNrxAcDBAUGBwEC"),
            new ProtocolTestData(
                name: "UserData_NoOptionalField",
                message: new UserDataMessage("user1",
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "kwildXNlcjGCpGpzb27EBwYHAQIDBAWrbWVzc2FnZXBhY2vEBwcBAgMEBQY="),
            new ProtocolTestData(
                name: "MultiUserData_NoOptionalField",
                message: new MultiUserDataMessage(new [] {"user1", "user2"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "kwmSpXVzZXIxpXVzZXIygqRqc29uxAcGBwECAwQFq21lc3NhZ2VwYWNrxAcHAQIDBAUG"),
            new ProtocolTestData(
                name: "Broadcast_NoOptionalField",
                message: new BroadcastDataMessage(new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["json"] = new byte[] {4, 5, 6, 7, 1, 2, 3},
                    ["messagepack"] = new byte[] {5, 6, 7, 1, 2, 3, 4}
                }),
                binary: "kwqQgqRqc29uxAcEBQYHAQIDq21lc3NhZ2VwYWNrxAcFBgcBAgME"),
            new ProtocolTestData(
                name: "BroadcastExcept_NoOptionalField",
                message: new BroadcastDataMessage(new[] {"conn7", "conn8", "conn9"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "kwqTpWNvbm43pWNvbm44pWNvbm45gqRqc29uxAcGBwECAwQFq21lc3NhZ2VwYWNrxAcHAQIDBAUG"),
            new ProtocolTestData(
                name: "JoinGroup_NoOptionalField",
                message: new JoinGroupMessage("conn10", "group1"),
                binary: "kwumY29ubjEwpmdyb3VwMQ=="),
            new ProtocolTestData(
                name: "LeaveGroup_NoOptionalField",
                message: new LeaveGroupMessage("conn11", "group2"),
                binary: "kwymY29ubjExpmdyb3VwMg=="),
            new ProtocolTestData(
                name: "UserJoinGroup_NoOptionalField",
                message: new UserJoinGroupMessage("conn10", "group1"),
                binary: "kxCmY29ubjEwpmdyb3VwMQ=="),
            new ProtocolTestData(
                name: "UserLeaveGroup_NoOptionalField",
                message: new UserLeaveGroupMessage("conn11", "group2"),
                binary: "kxGmY29ubjExpmdyb3VwMg=="),
            new ProtocolTestData(
                name: "GroupBroadcast_NoOptionalField",
                message: new GroupBroadcastDataMessage("group3",
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "lA2mZ3JvdXAzkIKkanNvbsQHBgcBAgMEBattZXNzYWdlcGFja8QHBwECAwQFBg=="),
            new ProtocolTestData(
                name: "GroupBroadcastExcept_NoOptionalField",
                message: new GroupBroadcastDataMessage("group3", new [] {"conn12", "conn13"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "lA2mZ3JvdXAzkqZjb25uMTKmY29ubjEzgqRqc29uxAcGBwECAwQFq21lc3NhZ2VwYWNrxAcHAQIDBAUG"),
            new ProtocolTestData(
                name: "MultiGroupBroadcast_NoOptionalField",
                message: new MultiGroupBroadcastDataMessage(new [] {"group4", "group5"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {1, 2, 3, 4, 5, 6, 7, 8},
                        ["messagepack"] = new byte[] {7, 8, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "kw6Spmdyb3VwNKZncm91cDWCpGpzb27ECAECAwQFBgcIq21lc3NhZ2VwYWNrxAgHCAECAwQFBg=="),
            new ProtocolTestData(
                name: "JoinGroupWithAck_NoOptionalField",
                message: new JoinGroupWithAckMessage("conn14", "group1", 1),
                binary: "lBKmY29ubjE0pmdyb3VwMQE="),
            new ProtocolTestData(
                name: "LeaveGroupWithAck_NoOptionalField",
                message: new LeaveGroupWithAckMessage("conn15", "group2", 1),
                binary: "lBOmY29ubjE1pmdyb3VwMgE="),
            new ProtocolTestData(
                name: "Ack_NoOptionalField",
                message: new AckMessage(1, 100),
                binary: "lBQBZKA="),
            new ProtocolTestData(
                name: "AckWithMessage_NoOptionalField",
                message: new AckMessage(2, 101, "Joined group successfully"),
                binary: "lBQCZblKb2luZWQgZ3JvdXAgc3VjY2Vzc2Z1bGx5"),
        }.ToDictionary(t => t.Name);

        public static IDictionary<string, ProtocolTestData> TestData => new[]
        {
            new ProtocolTestData(
                name: "HandshakeRequest",
                message: new HandshakeRequestMessage(1),
                binary: "lgEBAKAAgA=="),
            new ProtocolTestData(
                name: "HandshakeRequestWithProperty",
                message: new HandshakeRequestMessage(1) { ConnectionType = 1, Target = "abc" },
                binary: "lgEBAaNhYmMAgA=="),
            new ProtocolTestData(
                name: "HandshakeRequestWithMigratableStatus",
                message: new HandshakeRequestMessage(1) { MigrationLevel = 1},
                binary: "lgEBAKABgA=="),
            new ProtocolTestData(
                name: "HandshakeResponse",
                message: new HandshakeResponseMessage(),
                binary: "kwKggA=="),
            new ProtocolTestData(
                name: "HandshakeResponseWithError",
                message: new HandshakeResponseMessage("Version mismatch."),
                binary: "kwKxVmVyc2lvbiBtaXNtYXRjaC6A"),
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
                binary: "lgSlY29ubjGAgKCA"),
            new ProtocolTestData(
                name: "OpenConnectionWithClaims",
                message: new OpenConnectionMessage("conn2", new [] {new Claim(ClaimTypes.NameIdentifier, "user1")}),
                binary: "lgSlY29ubjKB2URodHRwOi8vc2NoZW1hcy54bWxzb2FwLm9yZy93cy8yMDA1LzA1L2lkZW50aXR5L2NsYWltcy9uYW1laWRlbnRpZmllcqV1c2VyMYCggA=="),
            new ProtocolTestData(
                name: "OpenConnectionWithHeaders",
                message: new OpenConnectionMessage("conn3", null, new Dictionary<string, StringValues>
                {
                    {"header-key-1", "header-value-1"},
                    {"header-key-2", new[] {"heaer-value-2a", "header-value-2b"}},
                    {"header-key-3", new[] {"heaer-value-3a", "header-value-3b", "header-value-3c"}}
                }, string.Empty),
                binary: "lgSlY29ubjOAg6xoZWFkZXIta2V5LTGRrmhlYWRlci12YWx1ZS0xrGhlYWRlci1rZXktMpKuaGVhZXItdmFsdWUtMmGvaGVhZGVyLXZhbHVlLTJirGhlYWRlci1rZXktM5OuaGVhZXItdmFsdWUtM2GvaGVhZGVyLXZhbHVlLTNir2hlYWRlci12YWx1ZS0zY6CA"),
            new ProtocolTestData(
                name: "OpenConnectionWithQueryString1",
                message: new OpenConnectionMessage("conn4", null, new Dictionary<string, StringValues>(), "query1=value1"),
                binary: "lgSlY29ubjSAgK1xdWVyeTE9dmFsdWUxgA=="),
            new ProtocolTestData(
                name: "OpenConnectionWithQueryString2",
                message: new OpenConnectionMessage("conn4", null, new Dictionary<string, StringValues>(), "query1=value1&query2=query2&query3=value3"),
                binary: "lgSlY29ubjSAgNkpcXVlcnkxPXZhbHVlMSZxdWVyeTI9cXVlcnkyJnF1ZXJ5Mz12YWx1ZTOA"),
            new ProtocolTestData(
                name: "OpenConnection_WithProtocol",
                message: new OpenConnectionMessage("conn1", null) { Protocol = "json" },
                binary: "lgSlY29ubjGAgKCBA6Rqc29u"),
            new ProtocolTestData(
                name: "CloseConnection",
                message: new CloseConnectionMessage("conn3"),
                binary: "lQWlY29ubjOggIA="),
            new ProtocolTestData(
                name: "CloseConnectionWithError",
                message: new CloseConnectionMessage("conn4", "Error message."),
                binary: "lQWlY29ubjSuRXJyb3IgbWVzc2FnZS6AgA=="),
            new ProtocolTestData(
                name: "ConnectionData",
                message: new ConnectionDataMessage("conn5", new byte[] {1, 2, 3, 4, 5, 6, 7}),
                binary: "lAalY29ubjXEBwECAwQFBgeA"),
            new ProtocolTestData(
                name: "MultiConnectionData",
                message: new MultiConnectionDataMessage(new [] {"conn6", "conn7"}, new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["json"] = new byte[] {2, 3, 4, 5, 6, 7, 1},
                    ["messagepack"] = new byte[] {3, 4, 5, 6, 7, 1, 2}
                }),
                binary: "lAeSpWNvbm42pWNvbm43gqRqc29uxAcCAwQFBgcBq21lc3NhZ2VwYWNrxAcDBAUGBwECgA=="),
            new ProtocolTestData(
                name: "UserData",
                message: new UserDataMessage("user1",
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "lAildXNlcjGCpGpzb27EBwYHAQIDBAWrbWVzc2FnZXBhY2vEBwcBAgMEBQaA"),
            new ProtocolTestData(
                name: "MultiUserData",
                message: new MultiUserDataMessage(new [] {"user1", "user2"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "lAmSpXVzZXIxpXVzZXIygqRqc29uxAcGBwECAwQFq21lc3NhZ2VwYWNrxAcHAQIDBAUGgA=="),
            new ProtocolTestData(
                name: "Broadcast",
                message: new BroadcastDataMessage(new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["json"] = new byte[] {4, 5, 6, 7, 1, 2, 3},
                    ["messagepack"] = new byte[] {5, 6, 7, 1, 2, 3, 4}
                }),
                binary: "lAqQgqRqc29uxAcEBQYHAQIDq21lc3NhZ2VwYWNrxAcFBgcBAgMEgA=="),
            new ProtocolTestData(
                name: "BroadcastExcept",
                message: new BroadcastDataMessage(new[] {"conn7", "conn8", "conn9"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "lAqTpWNvbm43pWNvbm44pWNvbm45gqRqc29uxAcGBwECAwQFq21lc3NhZ2VwYWNrxAcHAQIDBAUGgA=="),
            new ProtocolTestData(
                name: "JoinGroup",
                message: new JoinGroupMessage("conn10", "group1"),
                binary: "lAumY29ubjEwpmdyb3VwMYA="),
            new ProtocolTestData(
                name: "LeaveGroup",
                message: new LeaveGroupMessage("conn11", "group2"),
                binary: "lAymY29ubjExpmdyb3VwMoA="),
            new ProtocolTestData(
                name: "UserJoinGroup",
                message: new UserJoinGroupMessage("conn10", "group1"),
                binary: "lBCmY29ubjEwpmdyb3VwMYA="),
            new ProtocolTestData(
                name: "UserJoinGroupWithTtl",
                message: new UserJoinGroupMessage("conn10", "group1") { Ttl = 100 },
                binary: "lBCmY29ubjEwpmdyb3VwMYECZA=="),
            new ProtocolTestData(
                name: "UserLeaveGroup",
                message: new UserLeaveGroupMessage("conn11", "group2"),
                binary: "lBGmY29ubjExpmdyb3VwMoA="),
            new ProtocolTestData(
                name: "GroupBroadcast",
                message: new GroupBroadcastDataMessage("group3",
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "lQ2mZ3JvdXAzkIKkanNvbsQHBgcBAgMEBattZXNzYWdlcGFja8QHBwECAwQFBoA="),
            new ProtocolTestData(
                name: "GroupBroadcastExcept",
                message: new GroupBroadcastDataMessage("group3", new [] {"conn12", "conn13"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "lQ2mZ3JvdXAzkqZjb25uMTKmY29ubjEzgqRqc29uxAcGBwECAwQFq21lc3NhZ2VwYWNrxAcHAQIDBAUGgA=="),
            new ProtocolTestData(
                name: "MultiGroupBroadcast",
                message: new MultiGroupBroadcastDataMessage(new [] {"group4", "group5"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {1, 2, 3, 4, 5, 6, 7, 8},
                        ["messagepack"] = new byte[] {7, 8, 1, 2, 3, 4, 5, 6}
                    }),
                binary: "lA6Spmdyb3VwNKZncm91cDWCpGpzb27ECAECAwQFBgcIq21lc3NhZ2VwYWNrxAgHCAECAwQFBoA="),
            new ProtocolTestData(
                name: "ServiceError",
                message: new ServiceErrorMessage("Maximum message count limit reached: 100000"),
                binary: "kg/ZK01heGltdW0gbWVzc2FnZSBjb3VudCBsaW1pdCByZWFjaGVkOiAxMDAwMDA="),
            new ProtocolTestData(
                name: "JoinGroupWithAck",
                message: new JoinGroupWithAckMessage("conn14", "group1", 1),
                binary: "lRKmY29ubjE0pmdyb3VwMQGA"),
            new ProtocolTestData(
                name: "LeaveGroupWithAck",
                message: new LeaveGroupWithAckMessage("conn15", "group2", 1),
                binary: "lROmY29ubjE1pmdyb3VwMgGA"),
            new ProtocolTestData(
                name: "Ack",
                message: new AckMessage(1, 100),
                binary: "lRQBZKCA"),
            new ProtocolTestData(
                name: "AckWithMessage",
                message: new AckMessage(2, 101, "Joined group successfully"),
                binary: "lRQCZblKb2luZWQgZ3JvdXAgc3VjY2Vzc2Z1bGx5gA=="),

            // messages with tracing id
            new ProtocolTestData(
                name: "ConnectionDataWithTracingId",
                message: new ConnectionDataMessage("conn5", new byte[] {1, 2, 3, 4, 5, 6, 7}, tracingId: 1234L),
                binary: "lAalY29ubjXEBwECAwQFBgeBAc0E0g=="),
            new ProtocolTestData(
                name: "MultiConnectionDataWithTracingId",
                message: new MultiConnectionDataMessage(new [] {"conn6", "conn7"}, new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["json"] = new byte[] {2, 3, 4, 5, 6, 7, 1},
                    ["messagepack"] = new byte[] {3, 4, 5, 6, 7, 1, 2}
                }, tracingId: 1234L),
                binary: "lAeSpWNvbm42pWNvbm43gqRqc29uxAcCAwQFBgcBq21lc3NhZ2VwYWNrxAcDBAUGBwECgQHNBNI="),
            new ProtocolTestData(
                name: "UserDataWithTracingId",
                message: new UserDataMessage("user1",
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }, tracingId: 1234L),
                binary: "lAildXNlcjGCpGpzb27EBwYHAQIDBAWrbWVzc2FnZXBhY2vEBwcBAgMEBQaBAc0E0g=="),
            new ProtocolTestData(
                name: "MultiUserDataWithTracingId",
                message: new MultiUserDataMessage(new [] {"user1", "user2"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }, tracingId: 1234L),
                binary: "lAmSpXVzZXIxpXVzZXIygqRqc29uxAcGBwECAwQFq21lc3NhZ2VwYWNrxAcHAQIDBAUGgQHNBNI="),
            new ProtocolTestData(
                name: "BroadcastWithTracingId",
                message: new BroadcastDataMessage(new Dictionary<string, ReadOnlyMemory<byte>>
                {
                    ["json"] = new byte[] {4, 5, 6, 7, 1, 2, 3},
                    ["messagepack"] = new byte[] {5, 6, 7, 1, 2, 3, 4}
                }, tracingId: 1234L),
                binary: "lAqQgqRqc29uxAcEBQYHAQIDq21lc3NhZ2VwYWNrxAcFBgcBAgMEgQHNBNI="),
            new ProtocolTestData(
                name: "JoinGroupWithTracingId",
                message: new JoinGroupMessage("conn10", "group1", tracingId: 1234L),
                binary: "lAumY29ubjEwpmdyb3VwMYEBzQTS"),
            new ProtocolTestData(
                name: "LeaveGroupWithTracingId",
                message: new LeaveGroupMessage("conn11", "group2", tracingId: 1234L),
                binary: "lAymY29ubjExpmdyb3VwMoEBzQTS"),
            new ProtocolTestData(
                name: "UserJoinGroupWithTracingId",
                message: new UserJoinGroupMessage("conn10", "group1", tracingId: 1234L),
                binary: "lBCmY29ubjEwpmdyb3VwMYEBzQTS"),
            new ProtocolTestData(
                name: "UserLeaveGroupWithTracingId",
                message: new UserLeaveGroupMessage("conn11", "group2", tracingId: 1234L),
                binary: "lBGmY29ubjExpmdyb3VwMoEBzQTS"),
            new ProtocolTestData(
                name: "GroupBroadcastWithTracingId",
                message: new GroupBroadcastDataMessage("group3",
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {6, 7, 1, 2, 3, 4, 5},
                        ["messagepack"] = new byte[] {7, 1, 2, 3, 4, 5, 6}
                    }, tracingId: 1234L),
                binary: "lQ2mZ3JvdXAzkIKkanNvbsQHBgcBAgMEBattZXNzYWdlcGFja8QHBwECAwQFBoEBzQTS"),
            new ProtocolTestData(
                name: "MultiGroupBroadcastWithTracingId",
                message: new MultiGroupBroadcastDataMessage(new [] {"group4", "group5"},
                    new Dictionary<string, ReadOnlyMemory<byte>>
                    {
                        ["json"] = new byte[] {1, 2, 3, 4, 5, 6, 7, 8},
                        ["messagepack"] = new byte[] {7, 8, 1, 2, 3, 4, 5, 6}
                    }, tracingId: 1234L),
                binary: "lA6Spmdyb3VwNKZncm91cDWCpGpzb27ECAECAwQFBgcIq21lc3NhZ2VwYWNrxAgHCAECAwQFBoEBzQTS"),
            new ProtocolTestData(
                name: "JoinGroupWithAckWithTracingId",
                message: new JoinGroupWithAckMessage("conn14", "group1", 1, tracingId: 1234L),
                binary: "lRKmY29ubjE0pmdyb3VwMQGBAc0E0g=="),
            new ProtocolTestData(
                name: "LeaveGroupWithAckWithTracingId",
                message: new LeaveGroupWithAckMessage("conn15", "group2", 1, tracingId: 1234L),
                binary: "lROmY29ubjE1pmdyb3VwMgGBAc0E0g=="),
            new ProtocolTestData(
                name: "CheckUserInGroupWithAckWithMessage",
                message: new CheckUserInGroupWithAckMessage("user", "group", 3, 1234L),
                binary: "lRWkdXNlcqVncm91cAOBAc0E0g=="),
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
        public void WriteAndParseLargeData()
        {
            var protocol = new ServiceProtocol();
            var count = 70000;
            var str = new string(Enumerable.Range(0, count).Select(s => 'a').ToArray());
            var largeData = Encoding.UTF8.GetBytes(str);
            var message = new ConnectionDataMessage("abc", largeData);
            var bytes = protocol.GetMessageBytes(message);
            var seq = new ReadOnlySequence<byte>(bytes);
            var parsing = protocol.TryParseMessage(ref seq, out var result);
            Assert.Equal(count, (result as ConnectionDataMessage).Payload.Length);
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
            return (byte)(0x90 | size);
        }

        private static byte StringBytes(int size)
        {
            return (byte)(0xa0 | size);
        }

        private static byte MapBytes(int size)
        {
            return (byte)(0x80 | size);
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
