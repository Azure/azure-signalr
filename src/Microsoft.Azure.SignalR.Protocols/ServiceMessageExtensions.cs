// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    public static class ServiceMessageExtensions
    {
        public static TMessage AddClaims<TMessage>(this TMessage message, IEnumerable<Claim> claims) where TMessage : ServiceMessage
        {
            return message.AddOrUpdateArguments(ArgumentType.Claim, JsonConvert.SerializeObject(claims.Select(ClaimEntry.FromClaim)));
        }

        public static TMessage AddConnectionId<TMessage>(this TMessage message, string connectionId) where TMessage : ServiceMessage
        {
            return message.AddOrUpdateArguments(ArgumentType.ConnectionId, connectionId);
        }

        public static TMessage AddExcludedIds<TMessage>(this TMessage message, IReadOnlyList<string> excludedIds)
            where TMessage : ServiceMessage
        {
            return message.AddOrUpdateArguments(ArgumentType.ExcludedList, string.Join(",", excludedIds));
        }

        public static TMessage AddPayload<TMessage>(this TMessage message, string protocolName, byte[] payload) where TMessage : ServiceMessage
        {
            var payloads = message.GetPayloads();

            if (payloads.ContainsKey(protocolName))
            {
                payloads[protocolName] = payload;
            }
            else
            {
                payloads.Add(protocolName, payload);
            }
            return message;
        }

        public static TMessage AddProtocol<TMessage>(this TMessage message, string protocolName, int protocolVersion) where TMessage : ServiceMessage
        {
            return message.AddProtocolName(protocolName);
        }

        public static TMessage AddProtocolName<TMessage>(this TMessage message, string protocolName) where TMessage : ServiceMessage
        {
            return message.AddOrUpdateArguments(ArgumentType.ProtocolName, protocolName);
        }

        public static TMessage AddOrUpdateArguments<TMessage>(this TMessage message, ArgumentType argumentType, string value) where TMessage : ServiceMessage
        {
            var arguments = message.GetArguments();

            if (arguments.ContainsKey(argumentType))
            {
                arguments[argumentType] = value;
            }
            else
            {
                arguments.Add(argumentType, value);
            }
            return message;
        }

        public static TMessage AddSendConnection<TMessage>(this TMessage message, string connectionId) where TMessage : ServiceMessage
        {
            message.Command = CommandType.SendToConnection;
            return message.AddConnectionId(connectionId);
        }

        public static TMessage AddSendConnections<TMessage>(this TMessage message, IReadOnlyList<string> connectionIds) where TMessage : ServiceMessage
        {
            message.Command = CommandType.SendToConnections;
            return message.AddOrUpdateArguments(ArgumentType.ConnectionList, string.Join(",", connectionIds));
        }

        public static TMessage AddSendGroup<TMessage>(this TMessage message, string groupName) where TMessage : ServiceMessage
        {
            message.Command = CommandType.SendToGroup;
            return message.AddOrUpdateArguments(ArgumentType.GroupName, groupName);
        }

        public static TMessage AddSendGroups<TMessage>(this TMessage message, IReadOnlyList<string> groupNames) where TMessage : ServiceMessage
        {
            message.Command = CommandType.SendToGroups;
            return message.AddOrUpdateArguments(ArgumentType.GroupList, string.Join(",", groupNames));
        }

        public static TMessage AddSendGroupExcludedIds<TMessage>(this TMessage message, string groupName, IReadOnlyList<string> excludedIds) where TMessage : ServiceMessage
        {
            message.Command = CommandType.SendToGroup;
            return message.AddOrUpdateArguments(ArgumentType.GroupName, groupName)
                          .AddOrUpdateArguments(ArgumentType.ExcludedList, string.Join(",", excludedIds));
        }

        public static TMessage AddSendUserId<TMessage>(this TMessage message, string userId) where TMessage : ServiceMessage
        {
            message.Command = CommandType.SendToUser;
            return message.AddOrUpdateArguments(ArgumentType.UserId, userId);
        }

        public static TMessage AddSendUserIds<TMessage>(this TMessage message, IReadOnlyList<string> userIds) where TMessage : ServiceMessage
        {
            message.Command = CommandType.SendToUser;
            return message.AddOrUpdateArguments(ArgumentType.UserList, string.Join(",", userIds));
        }

        public static TMessage CreateAddConnection<TMessage>(this TMessage message, string connectionId, string protocolName, int protocolVersion, IEnumerable<Claim> claims) where TMessage : ServiceMessage
        {
            message.Command = CommandType.AddConnection;
            return message.AddConnectionId(connectionId)
                          .AddClaims(claims)
                          .AddProtocol(protocolName, protocolVersion);
        }

        public static TMessage CreateRemoveConnection<TMessage>(this TMessage message, string connectionId) where TMessage : ServiceMessage
        {
            message.Command = CommandType.RemoveConnection;
            return message.AddConnectionId(connectionId);
        }

        public static TMessage CreateAckResponse<TMessage>(this TMessage message, string connectionId, byte[] payload) where TMessage : ServiceMessage
        {
            message.Command = CommandType.AckMessage;
            message.AckPayload = payload;
            return message.AddConnectionId(connectionId);
        }

        public static TMessage CreateSendConnection<TMessage>(this TMessage message, string connectionId, string protocolName, byte[] payload) where TMessage : ServiceMessage
        {
            return message.AddSendConnection(connectionId)
                          .AddPayload(protocolName, payload);
        }

        public static TMessage CreateAddConnectionToGroup<TMessage>(this TMessage message, string connectionId, string groupName) where TMessage : ServiceMessage
        {
            message.Command = CommandType.AddConnectionToGroup;
            return message.AddConnectionId(connectionId)
                          .AddOrUpdateArguments(ArgumentType.GroupName, groupName);
        }

        public static TMessage CreateRemoveConnectionFromGroup<TMessage>(this TMessage message, string connectionId, string groupName) where TMessage : ServiceMessage
        {
            message.Command = CommandType.RemoveConnectionFromGroup;
            return message.AddConnectionId(connectionId)
                          .AddOrUpdateArguments(ArgumentType.GroupName, groupName);
        }

        public static string GetConnectionId<TMessage>(this TMessage message) where TMessage : ServiceMessage
        {
            if (message.GetArguments().TryGetValue(ArgumentType.ConnectionId, out string value))
            {
                return value;
            }
            return null;
        }

        public static string GetProtocolName<TMessage>(this TMessage message) where TMessage : ServiceMessage
        {
            if (message.GetArguments().TryGetValue(ArgumentType.ProtocolName, out string value))
            {
                return value;
            }
            return null;
        }

        public static bool TryGetClaims<TMessage>(this TMessage message, out IEnumerable<Claim> claims) where TMessage : ServiceMessage
        {
            claims = message.GetArguments().TryGetValue(ArgumentType.Claim, out var serializedClaims)
                    ? JsonConvert.DeserializeObject<IEnumerable<ClaimEntry>>(serializedClaims).Select(x => x.ToClaim())
                    : null;

            return claims != null;
        }

        public static bool TryGetConnectionId<TMessage>(this TMessage message, out string connectionId) where TMessage : ServiceMessage
        {
            return message.GetArguments().TryGetValue(ArgumentType.ConnectionId, out connectionId);
        }

        public static bool TryGetConnectionIds<TMessage>(this TMessage message, out IReadOnlyList<string> list) where TMessage : ServiceMessage
        {
            return message.TryGetList(ArgumentType.ConnectionList, out list);
        }

        public static bool TryGetExcludedIds<TMessage>(this TMessage message, out IReadOnlyList<string> list) where TMessage : ServiceMessage
        {
            return message.TryGetList(ArgumentType.ExcludedList, out list);
        }

        public static bool TryGetGroupName<TMessage>(this TMessage message, out string value) where TMessage : ServiceMessage
        {
            if (message.GetArguments().TryGetValue(ArgumentType.GroupName, out value))
            {
                return true;
            }
            return false;
        }

        public static bool TryGetGroupNames<TMessage>(this TMessage message, out IReadOnlyList<string> list) where TMessage : ServiceMessage
        {
            return message.TryGetList(ArgumentType.GroupList, out list);
        }

        public static bool TryGetUser<TMessage>(this TMessage message, out string value) where TMessage : ServiceMessage
        {
            return message.GetArguments().TryGetValue(ArgumentType.UserId, out value);
        }

        public static bool TryGetUsers<TMessage>(this TMessage message, out IReadOnlyList<string> list) where TMessage : ServiceMessage
        {
            return message.TryGetList(ArgumentType.UserList, out list);
        }

        private static bool TryGetListValues<TMessage>(this TMessage message, ArgumentType argumentType, out string listStr) where TMessage : ServiceMessage
        {
            if (message.GetArguments().TryGetValue(argumentType, out listStr))
            {
                return true;
            }
            return false;
        }

        private static bool TryGetList<TMessage>(this TMessage message, ArgumentType key, out IReadOnlyList<string> list) where TMessage : ServiceMessage
        {
            list = message.TryGetListValues(key, out var value)
                ? new List<string>(value.Split(','))
                : null;
            return list != null;
        }

        private static IDictionary<ArgumentType, string> GetArguments<TMessage>(this TMessage message) where TMessage : ServiceMessage
        {
            if (message.Arguments == null)
            {
                message.Arguments = new Dictionary<ArgumentType, string>();
            }
            return message.Arguments;
        }

        private static IDictionary<string, byte[]> GetPayloads<TMessage>(this TMessage message) where TMessage : ServiceMessage
        {
            if (message.Payloads == null)
            {
                message.Payloads = new Dictionary<string, byte[]>();
            }
            return message.Payloads;
        }
    }
}
