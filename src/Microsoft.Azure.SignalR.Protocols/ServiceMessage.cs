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

    // ConnectionMessage
    //      |----AddConnectionMessage
    //      |----RemoveConnectionMessage
    //      |----AbortConnectionMessage
    //      |----ConnectionDataMessage

    public abstract class ConnectionMessage : ServiceMessage
    {
        public string ConnectionId { get; set; }
    }

    public class AddConnectionMessage : ConnectionMessage
    {
        public Claim[] Claims { get; set; }
    }

    public class RemoveConnectionMessage : ConnectionMessage
    {
        public string ErrorMessage { get; set; }
    }

    public class AbortConnectionMessage : ConnectionMessage
    {
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

    // MulticastMessage
    //      |----MultiConnectionDataMessage
    //      |----BroadcastMessage
    //      |----GroupBroadcastMessage
    //      |----MultiGroupBroadcastMessage
    //      |----UserDataMessage
    //      |----MultiUserDataMessage

    public abstract class MulticastMessage : ServiceMessage
    {
        public IDictionary<string, byte[]> Payloads { get; set; }
    }

    public class MultiConnectionDataMessage : MulticastMessage
    {
        public string[] ConnectionList { get; set; }
    }

    public class BroadcastMessage : MulticastMessage
    {
        public string[] ExcludedList { get; set; }
    }

    public class GroupBroadcastMessage : MulticastMessage
    {
        public string GroupName { get; set; }
        public string[] ExcludedList { get; set; }
    }

    public class MultiGroupBroadcastMessage : MulticastMessage
    {
        public string[] GroupList { get; set; }
    }

    // It is possible that the same user has multiple connections. So it is a multi-cast message.
    public class UserDataMessage : MulticastMessage
    {
        public string UserId { get; set; }
    }

    public class MultiUserDataMessage : MulticastMessage
    {
        public string[] UserList { get; set; }
    }
}
