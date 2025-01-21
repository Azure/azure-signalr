// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;
using static Microsoft.Azure.SignalR.Protocol.MessagePackUtils;

namespace Microsoft.Azure.SignalR.Protocol;

/// <summary>
/// Represents a connection in a group.
/// </summary>
public record GroupMember : IMessagePackSerializable
{
    public string ConnectionId { get; set; } = string.Empty;

    public string? UserId { get; set; }

    void IMessagePackSerializable.Serialize(ref MessagePackWriter writer)
    {
        writer.WriteArrayHeader(2);
        writer.Write(ConnectionId);
        writer.Write(UserId);
    }

    void IMessagePackSerializable.Load(ref MessagePackReader reader, string fieldName)
    {
        _ = reader.ReadArrayHeader();
        ConnectionId = ReadStringNotNull(ref reader, nameof(ConnectionId));
        UserId = reader.ReadString();
    }
}
