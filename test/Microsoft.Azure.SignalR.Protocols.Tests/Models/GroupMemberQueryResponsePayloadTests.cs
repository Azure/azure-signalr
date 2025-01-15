// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.SignalR.Protocol;
using Xunit;

namespace Microsoft.Azure.SignalR.Protocols.Tests.Models;
public class GroupMemberQueryResponsePayloadTests
{
    [Fact]
    public void TestMessagePackSerialization()
    {
        var groupMembers = new List<GroupMember>
        {
            new GroupMember { ConnectionId = "conn1", UserId = "user1" },
            new GroupMember { ConnectionId = "conn2", UserId = "user2" }
        };
        var payload = new GroupMemberQueryResponse
        {
            Members = groupMembers,
            ContinuationToken = "token"
        };
        var buffer = new ArrayBufferWriter<byte>();
        var protocol = new ServiceProtocol();
        protocol.WriteMessagePayload(payload, buffer);
        var deserialized = protocol.ParseMessagePayload<GroupMemberQueryResponse>(new
            ReadOnlySequence<byte>(buffer.WrittenMemory));
        Assert.Equal(payload.ContinuationToken, deserialized.ContinuationToken);
        Assert.True(payload.Members.SequenceEqual(deserialized.Members));
    }
}
