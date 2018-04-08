using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    public static class ServiceMessageExtensions
    {
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
            if (message.Payloads.ContainsKey(protocolName))
            {
                message.Payloads[protocolName] = payload;
            }
            else
            {
                message.Payloads.Add(protocolName, payload);
            }
            return message;
        }

        public static TMessage AddOrUpdateArguments<TMessage>(this TMessage message, ArgumentType argumentType, string value) where TMessage : ServiceMessage
        {
            if (message.Arguments.ContainsKey(argumentType))
            {
                message.Arguments[argumentType] = value;
            }
            else
            {
                message.Arguments.Add(argumentType, value);
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
            if (message.Arguments.TryGetValue(ArgumentType.ConnectionId, out string value))
            {
                return value;
            }
            return null;
        }

        public static string GetProtocol<TMessage>(this TMessage message) where TMessage : ServiceMessage
        {
            if (message.Arguments.TryGetValue(ArgumentType.Protocol, out string value))
            {
                return value;
            }
            return null;
        }
    }
}
