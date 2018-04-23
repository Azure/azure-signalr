// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Protocol
{
    public abstract class MulticastDataMessage : ServiceMessage
    {
        protected MulticastDataMessage(IDictionary<string, ReadOnlyMemory<byte>> payloads)
        {
            Payloads = payloads;
        }

        public IDictionary<string, ReadOnlyMemory<byte>> Payloads { get; set; }
    }

    public class MultiConnectionDataMessage : MulticastDataMessage
    {
        public MultiConnectionDataMessage(IReadOnlyList<string> connectionList, IDictionary<string, ReadOnlyMemory<byte>> payloads) :
            base(payloads)
        {
            ConnectionList = connectionList;
        }

        public IReadOnlyList<string> ConnectionList { get; set; }
    }

    // It is possible that the same user has multiple connections. So it is a multi-cast message.
    public class UserDataMessage : MulticastDataMessage
    {
        public string UserId { get; set; }

        public UserDataMessage(string userId, IDictionary<string, ReadOnlyMemory<byte>> payloads) : base(payloads)
        {
            UserId = userId;
        }
    }

    public class MultiUserDataMessage : MulticastDataMessage
    {
        public MultiUserDataMessage(IReadOnlyList<string> userList, IDictionary<string, ReadOnlyMemory<byte>> payloads) : base(payloads)
        {
            UserList = userList;
        }

        public IReadOnlyList<string> UserList { get; set; }
    }

    public class BroadcastDataMessage : MulticastDataMessage
    {
        public IReadOnlyList<string> ExcludedList { get; set; }

        public BroadcastDataMessage(IReadOnlyList<string> excludedList, IDictionary<string, ReadOnlyMemory<byte>> payloads) : base(payloads)
        {
            ExcludedList = excludedList;
        }
    }

    public class GroupBroadcastDataMessage : MulticastDataMessage
    {
        public string GroupName { get; set; }
        public IReadOnlyList<string> ExcludedList { get; set; }

        public GroupBroadcastDataMessage(string groupName, IReadOnlyList<string> excludedList, IDictionary<string, ReadOnlyMemory<byte>> payloads)
            : base(payloads)
        {
            GroupName = groupName;
            ExcludedList = excludedList;
        }
    }

    public class MultiGroupBroadcastDataMessage : MulticastDataMessage
    {
        public IReadOnlyList<string> GroupList { get; set; }

        public MultiGroupBroadcastDataMessage(IReadOnlyList<string> groupList, IDictionary<string, ReadOnlyMemory<byte>> payloads) : base(payloads)
        {
            GroupList = groupList;
        }
    }
}
