// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using Microsoft.Azure.SignalR.Protocol;
using Xunit;

namespace Microsoft.Azure.SignalR.Protocols.Tests.Models;
public class GroupMemberTests
{
    [Fact]
    public void TestMessagePackSerialization()
    {
        var groupMember = new GroupMember() { ConnectionId = "conn", UserId = "userId" };
        var buffer = new ArrayBufferWriter<byte>();
        var protocol = new ServiceProtocol();
        protocol.WriteMessagePayload(groupMember, buffer);
        var deserialized = protocol.ParseMessagePayload<GroupMember>(new ReadOnlySequence<byte>(buffer.WrittenMemory));
        Assert.Equal(groupMember, deserialized);
    }
}
