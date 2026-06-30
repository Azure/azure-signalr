// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;

using MessagePack;

namespace Microsoft.Azure.SignalR.Protocol;

#nullable enable

/// <summary>
/// Wire model for the <see cref="GetConnectionClaimsMessage"/> ack payload.
/// The payload is either nil (no claims) or a MessagePack map of claim type to claim value.
/// </summary>
internal sealed class ConnectionClaimsResponse : IMessagePackSerializable
{
    public IReadOnlyList<Claim> Claims { get; set; } = [];

    void IMessagePackSerializable.Serialize(ref MessagePackWriter writer)
    {
        if (Claims.Count == 0)
        {
            writer.WriteNil();
            return;
        }

        writer.WriteMapHeader(Claims.Count);
        foreach (var claim in Claims)
        {
            writer.Write(claim.Type);
            writer.Write(claim.Value);
        }
    }

    void IMessagePackSerializable.Load(ref MessagePackReader reader, string fieldName)
    {
        if (reader.TryReadNil())
        {
            Claims = [];
            return;
        }

        var count = reader.ReadMapHeader();
        var claims = new List<Claim>(count);
        for (var i = 0; i < count; i++)
        {
            var type = reader.ReadString();
            var value = reader.ReadString();
            claims.Add(new Claim(type ?? string.Empty, value ?? string.Empty));
        }
        Claims = claims;
    }
}
