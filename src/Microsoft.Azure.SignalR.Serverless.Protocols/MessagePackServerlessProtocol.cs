// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System.Buffers;
using MessagePack;

namespace Microsoft.Azure.SignalR.Serverless.Protocols;

public class MessagePackServerlessProtocol : IServerlessProtocol
{
    public int Version => 1;

    public bool TryParseMessage(ref ReadOnlySequence<byte> input, out ServerlessMessage? message)
    {
        var reader = new MessagePackReader(input);
        _ = reader.ReadArrayHeader();
        var messageType = reader.ReadInt32("messageType");
        message = messageType switch
        {
            ServerlessProtocolConstants.InvocationMessageType => ConvertInvocationMessage(reader),
            _ => null,// TODO:OpenConnectionMessage and CloseConnectionMessage only will be sent in JSON format. It can be added later.
        };
        return message != null;
    }

    private static InvocationMessage ConvertInvocationMessage(MessagePackReader reader)
    {
        var invocationMessage = new InvocationMessage()
        {
            Type = ServerlessProtocolConstants.InvocationMessageType,
        };
        reader.SkipHeader();
        invocationMessage.InvocationId = reader.ReadString("invocationId");
        invocationMessage.Target = reader.ReadString("target");
        invocationMessage.Arguments = reader.ReadArray("arguments");
        return invocationMessage;
    }
}