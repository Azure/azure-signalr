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
    //      |----JoinGroupMessage
    //      |----LeaveGroupMessage

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

    public class JoinGroupMessage : ConnectionMessage
    {
        public string GroupName { get; set; }
    }

    public class LeaveGroupMessage : ConnectionMessage
    {
        public string GroupName { get; set; }
    }

    // MultiCastMessage
    //      |----MultiConnectionDataMessage
    //      |----BroadcastMessage
    //      |----GroupBroadcastMessage
    //      |----MultiGroupBroadcastMessage
    //      |----UserDataMessage
    //      |----MultiUserDataMessage

    public abstract class MultiCastMessage : ServiceMessage
    {
        public IDictionary<string, byte[]> Payloads { get; set; }
    }

    public class MultiConnectionDataMessage : MultiCastMessage
    {
        public string[] ConnectionList { get; set; }
    }

    public class BroadcastMessage : MultiCastMessage
    {
        public string[] ExcludedList { get; set; }
    }

    public class GroupBroadcastMessage : MultiCastMessage
    {
        public string[] ExcludedList { get; set; }
    }

    public class MultiGroupBroadcastMessage : MultiCastMessage
    {
        public string[] GroupList { get; set; }
    }

    // It is possible that the same user has multiple connections. So it is a multi-cast message.
    public class UserDataMessage : MultiCastMessage
    {
        public string UserId { get; set; }
    }

    public class MultiUserDataMessage : MultiCastMessage
    {
        public string[] UserList { get; set; }
    }
}
