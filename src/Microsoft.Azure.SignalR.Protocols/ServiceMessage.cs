﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    public enum CommandType
    {
        // Service -> SDK
        Ping = 0,
        AddConnection,
        RemoveConnection,
        AckMessage,
        // SDK -> Service
        AddConnectionToGroup,
        RemoveConnectionFromGroup,
        SendToConnection,
        SendToConnections,
        SendToAll,
        SendToAllExcept,
        SendToGroup,
        SendToGroups,
        SendToUser,
        SendToUsers,
        AbortConnection,
        SendToGroupExcept
    }

    public enum ArgumentType
    {
        ConnectionId = 0,
        Claim,
        ProtocolName,
        ConnectionList,
        GroupName,
        GroupList,
        UserId,
        UserList,
        ExcludedList
    }

    public class ServiceMessage
    {
        public static readonly string HandshakeProtocol = "json";

        public CommandType Command { get; set; }

        public IDictionary<ArgumentType, string> Arguments { get; set; }

        public byte[] AckPayload { get; set; }

        public IDictionary<string, byte[]> Payloads { get; set; }

        public static readonly ServiceMessage PingMessage = new ServiceMessage
        {
            Command = CommandType.Ping
        };
    }
}
