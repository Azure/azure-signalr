// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Protocol
{
    public abstract class MulticastDataMessage : ServiceMessage
    {
        protected MulticastDataMessage(IDictionary<string, byte[]> payloads)
        {
            Payloads = payloads;
        }

        public IDictionary<string, byte[]> Payloads { get; set; }
    }

    public class MultiConnectionDataMessage : MulticastDataMessage
    {
        public MultiConnectionDataMessage(string[] connectionList, IDictionary<string, byte[]> payloads) :
            base(payloads)
        {
            ConnectionList = connectionList;
        }

        public string[] ConnectionList { get; set; }
    }

    // It is possible that the same user has multiple connections. So it is a multi-cast message.
    public class UserDataMessage : MulticastDataMessage
    {
        public string UserId { get; set; }

        public UserDataMessage(string userId, IDictionary<string, byte[]> payloads) : base(payloads)
        {
            UserId = userId;
        }
    }

    public class MultiUserDataMessage : MulticastDataMessage
    {
        public MultiUserDataMessage(string[] userList, IDictionary<string, byte[]> payloads) : base(payloads)
        {
            UserList = userList;
        }

        public string[] UserList { get; set; }
    }

    public class BroadcastDataMessage : MulticastDataMessage
    {
        public string[] ExcludedList { get; set; }

        public BroadcastDataMessage(string[] excludedList, IDictionary<string, byte[]> payloads) : base(payloads)
        {
            ExcludedList = excludedList;
        }
    }

    public class GroupBroadcastDataMessage : MulticastDataMessage
    {
        public string GroupName { get; set; }
        public string[] ExcludedList { get; set; }

        public GroupBroadcastDataMessage(string groupName, string[] excludedList, IDictionary<string, byte[]> payloads)
            : base(payloads)
        {
            GroupName = groupName;
            ExcludedList = excludedList;
        }
    }

    public class MultiGroupBroadcastDataMessage : MulticastDataMessage
    {
        public string[] GroupList { get; set; }

        public MultiGroupBroadcastDataMessage(string[] groupList, IDictionary<string, byte[]> payloads) : base(payloads)
        {
            GroupList = groupList;
        }
    }
}
