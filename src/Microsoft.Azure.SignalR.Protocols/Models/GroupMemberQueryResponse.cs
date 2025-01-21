// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using MessagePack;

namespace Microsoft.Azure.SignalR.Protocol;

public sealed class GroupMemberQueryResponse : IMessagePackSerializable
{
    /// <summary>
    /// The group members.
    /// </summary>
    public IReadOnlyCollection<GroupMember> Members { get; set; } = [];

    /// <summary>
    /// A token that allows the client to retrieve the next page of results. 
    /// This parameter is provided by the service in the response of a previous request when there are additional results to be fetched. 
    /// Clients should include the continuationToken in the next request to receive the subsequent page of data. If this parameter is omitted, the server will return the first page of results.
    /// </summary>
    public string? ContinuationToken { get; set; }

    void IMessagePackSerializable.Serialize(ref MessagePackWriter writer)
    {
        writer.WriteArrayHeader(2);

        writer.WriteArrayHeader(Members.Count);
        foreach (var member in Members)
        {
            (member as IMessagePackSerializable).Serialize(ref writer);
        }
        writer.Write(ContinuationToken);
    }

    void IMessagePackSerializable.Load(ref MessagePackReader reader, string fieldName)
    {
        _ = reader.ReadArrayHeader();
        var memberCount = reader.ReadArrayHeader();
        var members = new List<GroupMember>(memberCount);
        for (var i = 0; i < memberCount; i++)
        {
            members.Add(reader.Deserialize<GroupMember>("groupMembers"));
        }
        Members = members;
        ContinuationToken = reader.ReadString();
    }
}
