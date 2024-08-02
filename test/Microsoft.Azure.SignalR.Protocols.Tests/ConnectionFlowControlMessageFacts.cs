// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Azure.SignalR.Protocol;
using Xunit;

namespace Microsoft.Azure.SignalR.Protocols.Tests;

public class ConnectionFlowControlMessageFacts
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData((int)ConnectionType.Server, 0)]
    [InlineData(0, (int)ConnectionFlowControlOperation.Offline)]
    public void TestThrowsInvalidDataException(int connectionType, int operation)
    {
        var message = new ConnectionFlowControlMessage(
            "conn1",
            (ConnectionFlowControlOperation)operation,
            (ConnectionType)connectionType);
        var protocol = new ServiceProtocol();
        var bytes = protocol.GetMessageBytes(message);

        var seq = new System.Buffers.ReadOnlySequence<byte>(bytes);
        Assert.Throws<InvalidDataException>(() => protocol.TryParseMessage(ref seq, out var result));
    }
}
