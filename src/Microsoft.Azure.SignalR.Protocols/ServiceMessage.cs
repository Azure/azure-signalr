// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.Protocol
{
    public abstract class ServiceMessage
    {
    }

    public class PingMessage : ServiceMessage
    {
        public static PingMessage Instance = new PingMessage();
    }

    // ConnectionMessage
    //      |----OpenConnectionMessage
    //      |----CloseConnectionMessage
    //      |----ConnectionDataMessage

    public abstract class ConnectionMessage : ServiceMessage
    {
        protected ConnectionMessage(string connectionId)
        {
            ConnectionId = connectionId;
        }

        public string ConnectionId { get; set; }
    }

    public class OpenConnectionMessage : ConnectionMessage
    {
        public OpenConnectionMessage(string connectionId, Claim[] claims) : base(connectionId)
        {
            Claims = claims;
        }

        public Claim[] Claims { get; set; }
    }

    public class CloseConnectionMessage : ConnectionMessage
    {
        public CloseConnectionMessage(string connectionId, string errorMessage) : base(connectionId)
        {
            ErrorMessage = errorMessage;
        }

        public string ErrorMessage { get; set; }
    }

    public class ConnectionDataMessage : ConnectionMessage
    {
        public ConnectionDataMessage(string connectionId, byte[] payload) : base(connectionId)
        {
            Payload = payload;
        }

        public byte[] Payload { get; set; }
    }

    // Group operation messages

    public class JoinGroupMessage : ServiceMessage
    {
        public string ConnectionId { get; set; }
        public string GroupName { get; set; }

        public JoinGroupMessage(string connectionId, string groupName)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
        }
    }

    public class LeaveGroupMessage : ServiceMessage
    {
        public string ConnectionId { get; set; }
        public string GroupName { get; set; }

        public LeaveGroupMessage(string connectionId, string groupName)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
        }
    }

    // MulticastDataMessage
    //      |----MultiConnectionDataMessage
    //      |----BroadcastDataMessage
    //      |----GroupBroadcastDataMessage
    //      |----MultiGroupBroadcastDataMessage
    //      |----UserDataMessage
    //      |----MultiUserDataMessage

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
}
