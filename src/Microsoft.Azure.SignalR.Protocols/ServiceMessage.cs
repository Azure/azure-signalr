// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR
{
    public abstract class ServiceMessage
    {
    }

    public class PingMessage : ServiceMessage
    {
    }

    // ConnectionMessage
    //      |----OpenConnectionMessage
    //      |----CloseConnectionMessage
    //      |----ConnectionDataMessage

    public abstract class ConnectionMessage : ServiceMessage
    {
        public string ConnectionId { get; set; }
    }

    public class OpenConnectionMessage : ConnectionMessage
    {
        public Claim[] Claims { get; set; }
    }

    public class CloseConnectionMessage : ConnectionMessage
    {
        public string ErrorMessage { get; set; }
    }

    public class ConnectionDataMessage : ConnectionMessage
    {
        public byte[] Payload { get; set; }
    }

    // Group operation messages

    public class JoinGroupMessage : ServiceMessage
    {
        public string ConnectionId { get; set; }
        public string GroupName { get; set; }
    }

    public class LeaveGroupMessage : ServiceMessage
    {
        public string ConnectionId { get; set; }
        public string GroupName { get; set; }
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
        public IDictionary<string, byte[]> Payloads { get; set; }
    }

    public class MultiConnectionDataMessage : MulticastDataMessage
    {
        public string[] ConnectionList { get; set; }
    }

    public class BroadcastDataMessage : MulticastDataMessage
    {
        public string[] ExcludedList { get; set; }
    }

    public class GroupBroadcastDataMessage : MulticastDataMessage
    {
        public string GroupName { get; set; }
        public string[] ExcludedList { get; set; }
    }

    public class MultiGroupBroadcastDataMessage : MulticastDataMessage
    {
        public string[] GroupList { get; set; }
    }

    // It is possible that the same user has multiple connections. So it is a multi-cast message.
    public class UserDataMessage : MulticastDataMessage
    {
        public string UserId { get; set; }
    }

    public class MultiUserDataMessage : MulticastDataMessage
    {
        public string[] UserList { get; set; }
    }
}
