// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Protocol.Tests
{
    public class ServiceMessageEqualityComparer : IEqualityComparer<ServiceMessage>
    {
        public static readonly ServiceMessageEqualityComparer Instance = new ServiceMessageEqualityComparer();

        public bool Equals(ServiceMessage x, ServiceMessage y)
        {
            if (x.GetType() != y.GetType())
            {
                return false;
            }

#pragma warning disable CS0618 // Type or member is obsolete
            switch (x)
            {
                case HandshakeRequestMessage handshakeRequestMessage:
                    return HandshakeRequestMessagesEqual(handshakeRequestMessage, (HandshakeRequestMessage)y);
                case HandshakeResponseMessage handshakeResponseMessage:
                    return HandshakeResponseMessagesEqual(handshakeResponseMessage, (HandshakeResponseMessage)y);
                case AccessKeyRequestMessage accessKeyRequestMessage:
                    return AccessKeyRequestMessageEqual(accessKeyRequestMessage, (AccessKeyRequestMessage)y);
                case AccessKeyResponseMessage accessKeyResponseMessage:
                    return AccessKeyResponseMessageEqual(accessKeyResponseMessage, (AccessKeyResponseMessage)y);
                case PingMessage _:
                    return y is PingMessage;
                case OpenConnectionMessage openConnectionMessage:
                    return OpenConnectionMessagesEqual(openConnectionMessage, (OpenConnectionMessage)y);
                case CloseConnectionMessage closeConnectionMessage:
                    return CloseConnectionMessagesEqual(closeConnectionMessage, (CloseConnectionMessage)y);
                case CloseConnectionWithAckMessage closeConnectionWithAckMessage:
                    return CloseConnectionWithAckMessagesEqual(closeConnectionWithAckMessage, (CloseConnectionWithAckMessage)y);
                case CloseConnectionsWithAckMessage closeConnectionsWithAckMessage:
                    return CloseConnectionsWithAckMessagesEqual(closeConnectionsWithAckMessage, (CloseConnectionsWithAckMessage)y);
                case CloseUserConnectionsWithAckMessage closeUserConnectionsWithAckMessage:
                    return CloseUserConnectionsWithAckMessagesEqual(closeUserConnectionsWithAckMessage, (CloseUserConnectionsWithAckMessage)y);
                case CloseGroupConnectionsWithAckMessage closeGroupConnectionsWithAckMessage:
                    return CloseGroupConnectionsWithAckMessagesEqual(closeGroupConnectionsWithAckMessage, (CloseGroupConnectionsWithAckMessage)y);
                case ConnectionDataMessage connectionDataMessage:
                    return ConnectionDataMessagesEqual(connectionDataMessage, (ConnectionDataMessage)y);
                case ConnectionReconnectMessage connectionReconnectMessage:
                    return ConnectionReconnectMessagesEqual(connectionReconnectMessage, (ConnectionReconnectMessage)y);
                case MultiConnectionDataMessage multiConnectionDataMessage:
                    return MultiConnectionDataMessagesEqual(multiConnectionDataMessage, (MultiConnectionDataMessage)y);
                case UserDataMessage userDataMessage:
                    return UserDataMessagesEqual(userDataMessage, (UserDataMessage)y);
                case MultiUserDataMessage multiUserDataMessage:
                    return MultiUserDataMessagesEqual(multiUserDataMessage, (MultiUserDataMessage)y);
                case BroadcastDataMessage broadcastDataMessage:
                    return BroadcastDataMessagesEqual(broadcastDataMessage, (BroadcastDataMessage)y);
                case JoinGroupMessage joinGroupMessage:
                    return JoinGroupMessagesEqual(joinGroupMessage, (JoinGroupMessage)y);
                case LeaveGroupMessage leaveGroupMessage:
                    return LeaveGroupMessagesEqual(leaveGroupMessage, (LeaveGroupMessage)y);
                case UserJoinGroupMessage userJoinGroupMessage:
                    return UserJoinGroupMessagesEqual(userJoinGroupMessage, (UserJoinGroupMessage)y);
                case UserLeaveGroupMessage userLeaveGroupMessage:
                    return UserLeaveGroupMessagesEqual(userLeaveGroupMessage, (UserLeaveGroupMessage)y);
                case UserJoinGroupWithAckMessage userJoinGroupWithAckMessage:
                    return UserJoinGroupWithAckMessagesEqual(userJoinGroupWithAckMessage, (UserJoinGroupWithAckMessage)y);
                case UserLeaveGroupWithAckMessage userLeaveGroupWithAckMessage:
                    return UserLeaveGroupWithAckMessagesEqual(userLeaveGroupWithAckMessage, (UserLeaveGroupWithAckMessage)y);
                case GroupBroadcastDataMessage groupBroadcastDataMessage:
                    return GroupBroadcastDataMessagesEqual(groupBroadcastDataMessage, (GroupBroadcastDataMessage)y);
                case MultiGroupBroadcastDataMessage multiGroupBroadcastDataMessage:
                    return MultiGroupBroadcastDataMessagesEqual(multiGroupBroadcastDataMessage,
                        (MultiGroupBroadcastDataMessage)y);
                case ServiceErrorMessage serviceErrorMessage:
                    return ServiceErrorMessageEqual(serviceErrorMessage, (ServiceErrorMessage)y);
                case JoinGroupWithAckMessage joinGroupWithAckMessage:
                    return JoinGroupWithAckMessageEqual(joinGroupWithAckMessage, (JoinGroupWithAckMessage)y);
                case LeaveGroupWithAckMessage leaveGroupWithAckMessage:
                    return LeaveGroupWithAckMessageEqual(leaveGroupWithAckMessage, (LeaveGroupWithAckMessage)y);
                case CheckUserInGroupWithAckMessage checkUserInGroupWithAckMessage:
                    return CheckUserInGroupWithAckMessageEqual(checkUserInGroupWithAckMessage, (CheckUserInGroupWithAckMessage)y);
                case CheckGroupExistenceWithAckMessage checkAnyConnectionInGroupWithAckMessage:
                    return CheckGroupExistenceWithAckMessageEqual(checkAnyConnectionInGroupWithAckMessage, (CheckGroupExistenceWithAckMessage)y);
                case CheckConnectionExistenceWithAckMessage checkConnectionExistenceWithAckMessage:
                    return CheckConnectionExistenceWithAckMessageEqual(checkConnectionExistenceWithAckMessage, (CheckConnectionExistenceWithAckMessage)y);
                case CheckUserExistenceWithAckMessage checkConnectionExistenceAsUserWithAckMessage:
                    return CheckUserExistenceWithAckMessageEqual(checkConnectionExistenceAsUserWithAckMessage, (CheckUserExistenceWithAckMessage)y);
                case AckMessage ackMessage:
                    return AckMessageEqual(ackMessage, (AckMessage)y);
                case ServiceEventMessage serviceWarningMessage:
                    return ServiceWarningMessageEqual(serviceWarningMessage, (ServiceEventMessage)y);
                case ClientInvocationMessage clientInvocationMessage:
                    return ClientInvocationMessageEuqal(clientInvocationMessage, (ClientInvocationMessage)y);
                case ClientCompletionMessage clientCompletionMessage:
                    return ClientCompletionMessageEqual(clientCompletionMessage, (ClientCompletionMessage)y);
                case ErrorCompletionMessage errorCompletionMessage:
                    return ErrorCompletionMessageEqual(errorCompletionMessage, (ErrorCompletionMessage)y);
                case ServiceMappingMessage serviceMappingMessage:
                    return ServiceMappingMessageEqual(serviceMappingMessage, (ServiceMappingMessage)y);
                case ConnectionFlowControlMessage connectionFlowControlMessage:
                    return ConnectionFlowControlMessageEqual(connectionFlowControlMessage, (ConnectionFlowControlMessage)y);
                case GroupMemberQueryMessage groupMemberQueryMessage:
                    return GroupMemberQueryMessageEqual(groupMemberQueryMessage, (GroupMemberQueryMessage)y);
                default:
                    throw new InvalidOperationException($"Unknown message type: {x.GetType().FullName}");
            }
#pragma warning restore CS0618 // Type or member is obsolete
        }

        public int GetHashCode(ServiceMessage obj)
        {
            return 0;
        }

        private bool HandshakeRequestMessagesEqual(HandshakeRequestMessage x, HandshakeRequestMessage y)
        {
            return x.Version == y.Version &&
                x.AllowStatefulReconnects == y.AllowStatefulReconnects &&
                x.MigrationLevel == y.MigrationLevel &&
                (x.Target ?? string.Empty) == (y.Target ?? string.Empty) &&
                x.ConnectionType == y.ConnectionType;
        }

        private bool HandshakeResponseMessagesEqual(HandshakeResponseMessage x, HandshakeResponseMessage y)
        {
            return StringEqual(x.ErrorMessage, y.ErrorMessage) &&
                StringEqual(x.ConnectionId, y.ConnectionId);
        }

        private bool AccessKeyRequestMessageEqual(AccessKeyRequestMessage x, AccessKeyRequestMessage y)
        {
            return StringEqual(x.Token, y.Token) &&
                StringEqual(x.Kid, y.Kid);
        }

        private bool AccessKeyResponseMessageEqual(AccessKeyResponseMessage x, AccessKeyResponseMessage y)
        {
            return StringEqual(x.Kid, y.Kid) &&
                StringEqual(x.AccessKey, y.AccessKey) &&
                StringEqual(x.ErrorType, y.ErrorType) &&
                StringEqual(x.ErrorMessage, y.ErrorMessage);
        }

        private bool OpenConnectionMessagesEqual(OpenConnectionMessage x, OpenConnectionMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                   ClaimsEqual(x.Claims, y.Claims) &&
                   HeadersEqual(x.Headers, y.Headers) &&
                   StringEqual(x.QueryString, y.QueryString) &&
                   StringEqual(x.Protocol, y.Protocol) &&
                   x.TracingId == y.TracingId;
        }

        private bool CloseConnectionMessagesEqual(CloseConnectionMessage x, CloseConnectionMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) && StringEqual(x.ErrorMessage, y.ErrorMessage) && x.TracingId == y.TracingId;
        }

#pragma warning disable CS0618 // Type or member is obsolete
        private bool CloseConnectionWithAckMessagesEqual(CloseConnectionWithAckMessage x, CloseConnectionWithAckMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) && StringEqual(x.Reason, y.Reason)
                && x.TracingId == y.TracingId && x.AckId == y.AckId;
        }

        private bool CloseConnectionsWithAckMessagesEqual(CloseConnectionsWithAckMessage x, CloseConnectionsWithAckMessage y)
        {
            return SequenceEqual(x.ExcludedList, y.ExcludedList) && StringEqual(x.Reason, y.Reason)
                && x.TracingId == y.TracingId && x.AckId == y.AckId;
        }
#pragma warning restore CS0618 // Type or member is obsolete

        private bool CloseUserConnectionsWithAckMessagesEqual(CloseUserConnectionsWithAckMessage x, CloseUserConnectionsWithAckMessage y)
        {
            return SequenceEqual(x.ExcludedList, y.ExcludedList) && StringEqual(x.Reason, y.Reason) && StringEqual(x.UserId, y.UserId)
                && x.TracingId == y.TracingId && x.AckId == y.AckId;
        }

        private bool CloseGroupConnectionsWithAckMessagesEqual(CloseGroupConnectionsWithAckMessage x, CloseGroupConnectionsWithAckMessage y)
        {
            return SequenceEqual(x.ExcludedList, y.ExcludedList) && StringEqual(x.Reason, y.Reason) && StringEqual(x.GroupName, y.GroupName)
                && x.TracingId == y.TracingId && x.AckId == y.AckId;
        }

        private bool ConnectionDataMessagesEqual(ConnectionDataMessage x, ConnectionDataMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                SequenceEqual(x.Payload.ToArray(), y.Payload.ToArray()) &&
                x.TracingId == y.TracingId &&
                x.Type == y.Type &&
                x.IsPartial == y.IsPartial;
        }

        private bool ConnectionReconnectMessagesEqual(ConnectionReconnectMessage x, ConnectionReconnectMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId);
        }

        private bool MultiConnectionDataMessagesEqual(MultiConnectionDataMessage x, MultiConnectionDataMessage y)
        {
            return SequenceEqual(x.ConnectionList, y.ConnectionList) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   x.TracingId == y.TracingId;
        }

        private bool UserDataMessagesEqual(UserDataMessage x, UserDataMessage y)
        {
            return StringEqual(x.UserId, y.UserId) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   x.TracingId == y.TracingId;
        }

        private bool MultiUserDataMessagesEqual(MultiUserDataMessage x, MultiUserDataMessage y)
        {
            return SequenceEqual(x.UserList, y.UserList) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   x.TracingId == y.TracingId;
        }

        private bool BroadcastDataMessagesEqual(BroadcastDataMessage x, BroadcastDataMessage y)
        {
            return SequenceEqual(x.ExcludedList, y.ExcludedList) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   x.TracingId == y.TracingId;
        }

        private bool JoinGroupMessagesEqual(JoinGroupMessage x, JoinGroupMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.TracingId == y.TracingId;
        }

        private bool LeaveGroupMessagesEqual(LeaveGroupMessage x, LeaveGroupMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.TracingId == y.TracingId;
        }

        private bool UserJoinGroupMessagesEqual(UserJoinGroupMessage x, UserJoinGroupMessage y)
        {
            return StringEqual(x.UserId, y.UserId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.TracingId == y.TracingId &&
                   x.Ttl == y.Ttl;
        }

        private bool UserLeaveGroupMessagesEqual(UserLeaveGroupMessage x, UserLeaveGroupMessage y)
        {
            return StringEqual(x.UserId, y.UserId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.TracingId == y.TracingId;
        }

        private bool UserJoinGroupWithAckMessagesEqual(UserJoinGroupWithAckMessage x, UserJoinGroupWithAckMessage y)
        {
            return StringEqual(x.UserId, y.UserId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.TracingId == y.TracingId &&
                   x.Ttl == y.Ttl &&
                   x.AckId == y.AckId;
        }

        private bool UserLeaveGroupWithAckMessagesEqual(UserLeaveGroupWithAckMessage x, UserLeaveGroupWithAckMessage y)
        {
            return StringEqual(x.UserId, y.UserId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.TracingId == y.TracingId &&
                   x.AckId == y.AckId;
        }

        private bool GroupBroadcastDataMessagesEqual(GroupBroadcastDataMessage x, GroupBroadcastDataMessage y)
        {
            return StringEqual(x.GroupName, y.GroupName) &&
                   StringEqual(x.CallerUserId, y.CallerUserId) &&
                   SequenceEqual(x.ExcludedList, y.ExcludedList) &&
                   SequenceEqual(x.ExcludedUserList, y.ExcludedUserList) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   x.TracingId == y.TracingId;
        }

        private bool MultiGroupBroadcastDataMessagesEqual(MultiGroupBroadcastDataMessage x,
            MultiGroupBroadcastDataMessage y)
        {
            return SequenceEqual(x.GroupList, y.GroupList) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   x.TracingId == y.TracingId;
        }

        private bool ServiceErrorMessageEqual(ServiceErrorMessage x, ServiceErrorMessage y)
        {
            return StringEqual(x.ErrorMessage, y.ErrorMessage);
        }

        private bool JoinGroupWithAckMessageEqual(JoinGroupWithAckMessage x, JoinGroupWithAckMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.AckId == y.AckId &&
                   x.TracingId == y.TracingId;
        }

        private bool LeaveGroupWithAckMessageEqual(LeaveGroupWithAckMessage x, LeaveGroupWithAckMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.AckId == y.AckId &&
                   x.TracingId == y.TracingId;
        }

        private bool CheckUserInGroupWithAckMessageEqual(CheckUserInGroupWithAckMessage x, CheckUserInGroupWithAckMessage y)
        {
            return StringEqual(x.UserId, y.UserId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.AckId == y.AckId &&
                   x.TracingId == y.TracingId;
        }

        private bool CheckGroupExistenceWithAckMessageEqual(CheckGroupExistenceWithAckMessage x, CheckGroupExistenceWithAckMessage y)
        {
            return StringEqual(x.GroupName, y.GroupName) &&
                   x.AckId == y.AckId &&
                   x.TracingId == y.TracingId;
        }

        private bool CheckConnectionExistenceWithAckMessageEqual(CheckConnectionExistenceWithAckMessage x, CheckConnectionExistenceWithAckMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                   x.AckId == y.AckId &&
                   x.TracingId == y.TracingId;
        }

        private bool CheckUserExistenceWithAckMessageEqual(CheckUserExistenceWithAckMessage x, CheckUserExistenceWithAckMessage y)
        {
            return StringEqual(x.UserId, y.UserId) &&
                   x.AckId == y.AckId &&
                   x.TracingId == y.TracingId;
        }

        private bool AckMessageEqual(AckMessage x, AckMessage y)
        {
            return x.AckId == y.AckId &&
                   x.Status == y.Status &&
                   x.Payload.HasValue == y.Payload.HasValue && SequenceEqual(x.Payload.GetValueOrDefault().ToArray(), y.Payload.GetValueOrDefault().ToArray()) &&
#pragma warning disable CS0618 // Type or member is obsolete
                   StringEqual(x.Message, y.Message);
#pragma warning restore CS0618 // Type or member is obsolete
        }

        private bool ServiceWarningMessageEqual(ServiceEventMessage x, ServiceEventMessage y)
        {
            return x.Type == y.Type &&
                StringEqual(x.Id, y.Id) &&
                x.Kind == y.Kind &&
                StringEqual(x.Message, y.Message);
        }

        private bool ClientInvocationMessageEuqal(ClientInvocationMessage x, ClientInvocationMessage y)
        {
            return StringEqual(x.InvocationId, y.InvocationId) &&
                StringEqual(x.ConnectionId, y.ConnectionId) &&
                StringEqual(x.CallerServerId, y.CallerServerId) &&
                PayloadsEqual(x.Payloads, y.Payloads);
        }

        private bool ClientCompletionMessageEqual(ClientCompletionMessage x, ClientCompletionMessage y)
        {
            return StringEqual(x.InvocationId, y.InvocationId) &&
                StringEqual(x.ConnectionId, y.ConnectionId) &&
                StringEqual(x.CallerServerId, y.CallerServerId) &&
                StringEqual(x.Protocol, y.Protocol) &&
                SequenceEqual(x.Payload.ToArray(), y.Payload.ToArray());
        }

        private bool ErrorCompletionMessageEqual(ErrorCompletionMessage x, ErrorCompletionMessage y)
        {
            return StringEqual(x.InvocationId, y.InvocationId) &&
                StringEqual(x.ConnectionId, y.ConnectionId) &&
                StringEqual(x.CallerServerId, y.CallerServerId) &&
                StringEqual(x.Error, y.Error);
        }

        private bool ServiceMappingMessageEqual(ServiceMappingMessage x, ServiceMappingMessage y)
        {
            return StringEqual(x.InvocationId, y.InvocationId) &&
                StringEqual(x.ConnectionId, y.ConnectionId) &&
                StringEqual(x.InstanceId, y.InstanceId);
        }

        private bool ConnectionFlowControlMessageEqual(ConnectionFlowControlMessage x, ConnectionFlowControlMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                Equals(x.ConnectionType, y.ConnectionType) &&
                Equals(x.Operation, y.Operation);
        }

        private bool GroupMemberQueryMessageEqual(GroupMemberQueryMessage x, GroupMemberQueryMessage y)
        {
            return x.AckId == y.AckId &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.Max == y.Max &&
                   StringEqual(x.ContinuationToken, y.ContinuationToken) &&
                   x.TracingId == y.TracingId;
        }

        private static bool StringEqual(string x, string y)
        {
            return string.Equals(x, y, StringComparison.Ordinal);
        }

        private static bool StringEqualIgnoreCase(string x, string y)
        {
            return string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ClaimsEqual(Claim[] x, Claim[] y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x?.Length != y?.Length)
            {
                return false;
            }

            return !x.Where((t, i) => t.Type != y[i].Type || !StringEqual(t.Value, y[i].Value)).Any();
        }

        private static bool PayloadsEqual(IDictionary<string, ReadOnlyMemory<byte>> x,
            IDictionary<string, ReadOnlyMemory<byte>> y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x?.Count != y?.Count)
            {
                return false;
            }

            for (int i = 0; i < x.Count; i++)
            {
                if (!StringEqual(x.ElementAt(i).Key, y.ElementAt(i).Key) ||
                    !SequenceEqual(x.ElementAt(i).Value.ToArray(), y.ElementAt(i).Value.ToArray()))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HeadersEqual(IDictionary<string, StringValues> x, IDictionary<string, StringValues> y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x?.Count != y?.Count)
            {
                return false;
            }

            for (int i = 0; i < x.Count; i++)
            {
                var xKey = x.ElementAt(i).Key;
                var yKey = y.ElementAt(i).Key;
                if (!StringEqualIgnoreCase(xKey, yKey) ||
                    !SequenceEqual(x[xKey], y[yKey]) ||
                    !SequenceEqual(x[xKey], y[yKey.ToUpper()]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SequenceEqual(object left, object right)
        {
            if (left == null && right == null)
            {
                return true;
            }

            var leftEnumerable = left as IEnumerable;
            var rightEnumerable = right as IEnumerable;
            if (leftEnumerable == null || rightEnumerable == null)
            {
                return false;
            }

            var leftEnumerator = leftEnumerable.GetEnumerator();
            var rightEnumerator = rightEnumerable.GetEnumerator();
            var leftMoved = leftEnumerator.MoveNext();
            var rightMoved = rightEnumerator.MoveNext();
            for (; leftMoved && rightMoved; leftMoved = leftEnumerator.MoveNext(), rightMoved = rightEnumerator.MoveNext())
            {
                if (!Equals(leftEnumerator.Current, rightEnumerator.Current))
                {
                    return false;
                }
            }

            return !leftMoved && !rightMoved;
        }
    }
}