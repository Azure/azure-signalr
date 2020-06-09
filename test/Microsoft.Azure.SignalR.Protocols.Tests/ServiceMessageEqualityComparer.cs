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

            switch (x)
            {
                case HandshakeRequestMessage handshakeRequestMessage:
                    return HandshakeRequestMessagesEqual(handshakeRequestMessage, (HandshakeRequestMessage)y);
                case HandshakeResponseMessage handshakeResponseMessage:
                    return HandshakeResponseMessagesEqual(handshakeResponseMessage, (HandshakeResponseMessage)y);
                case PingMessage _:
                    return y is PingMessage;
                case OpenConnectionMessage openConnectionMessage:
                    return OpenConnectionMessagesEqual(openConnectionMessage, (OpenConnectionMessage)y);
                case CloseConnectionMessage closeConnectionMessage:
                    return CloseConnectionMessagesEqual(closeConnectionMessage, (CloseConnectionMessage)y);
                case ConnectionDataMessage connectionDataMessage:
                    return ConnectionDataMessagesEqual(connectionDataMessage, (ConnectionDataMessage)y);
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
                case AckMessage ackMessage:
                    return AckMessageEqual(ackMessage, (AckMessage)y);
                default:
                    throw new InvalidOperationException($"Unknown message type: {x.GetType().FullName}");
            }
        }

        public int GetHashCode(ServiceMessage obj)
        {
            return 0;
        }

        private bool HandshakeRequestMessagesEqual(HandshakeRequestMessage x, HandshakeRequestMessage y)
        {
            return x.Version == y.Version;
        }

        private bool HandshakeResponseMessagesEqual(HandshakeResponseMessage x, HandshakeResponseMessage y)
        {
            return StringEqual(x.ErrorMessage, y.ErrorMessage);
        }

        private bool OpenConnectionMessagesEqual(OpenConnectionMessage x, OpenConnectionMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                   ClaimsEqual(x.Claims, y.Claims) &&
                   HeadersEqual(x.Headers, y.Headers) &&
                   StringEqual(x.QueryString, y.QueryString);
        }

        private bool CloseConnectionMessagesEqual(CloseConnectionMessage x, CloseConnectionMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) && StringEqual(x.ErrorMessage, y.ErrorMessage);
        }

        private bool ConnectionDataMessagesEqual(ConnectionDataMessage x, ConnectionDataMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                SequenceEqual(x.Payload.ToArray(), y.Payload.ToArray()) &&
                StringEqual(x.TracingId, y.TracingId);
        }

        private bool MultiConnectionDataMessagesEqual(MultiConnectionDataMessage x, MultiConnectionDataMessage y)
        {
            return SequenceEqual(x.ConnectionList, y.ConnectionList) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   StringEqual(x.TracingId, y.TracingId);
        }

        private bool UserDataMessagesEqual(UserDataMessage x, UserDataMessage y)
        {
            return StringEqual(x.UserId, y.UserId) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   StringEqual(x.TracingId, y.TracingId);
        }

        private bool MultiUserDataMessagesEqual(MultiUserDataMessage x, MultiUserDataMessage y)
        {
            return SequenceEqual(x.UserList, y.UserList) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   StringEqual(x.TracingId, y.TracingId);
        }

        private bool BroadcastDataMessagesEqual(BroadcastDataMessage x, BroadcastDataMessage y)
        {
            return SequenceEqual(x.ExcludedList, y.ExcludedList) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   StringEqual(x.TracingId, y.TracingId);
        }

        private bool JoinGroupMessagesEqual(JoinGroupMessage x, JoinGroupMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) && 
                   StringEqual(x.GroupName, y.GroupName) &&
                   StringEqual(x.TracingId, y.TracingId);
        }

        private bool LeaveGroupMessagesEqual(LeaveGroupMessage x, LeaveGroupMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) && 
                   StringEqual(x.GroupName, y.GroupName) &&
                   StringEqual(x.TracingId, y.TracingId);
        }

        private bool UserJoinGroupMessagesEqual(UserJoinGroupMessage x, UserJoinGroupMessage y)
        {
            return StringEqual(x.UserId, y.UserId) && 
                   StringEqual(x.GroupName, y.GroupName) &&
                   StringEqual(x.TracingId, y.TracingId) &&
                   x.Ttl == y.Ttl;
        }

        private bool UserLeaveGroupMessagesEqual(UserLeaveGroupMessage x, UserLeaveGroupMessage y)
        {
            return StringEqual(x.UserId, y.UserId) && 
                   StringEqual(x.GroupName, y.GroupName) &&
                   StringEqual(x.TracingId, y.TracingId);
        }
        private bool GroupBroadcastDataMessagesEqual(GroupBroadcastDataMessage x, GroupBroadcastDataMessage y)
        {
            return StringEqual(x.GroupName, y.GroupName) &&
                   SequenceEqual(x.ExcludedList, y.ExcludedList) &&
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   StringEqual(x.TracingId, y.TracingId);
        }

        private bool MultiGroupBroadcastDataMessagesEqual(MultiGroupBroadcastDataMessage x,
            MultiGroupBroadcastDataMessage y)
        {
            return SequenceEqual(x.GroupList, y.GroupList) && 
                   PayloadsEqual(x.Payloads, y.Payloads) &&
                   StringEqual(x.TracingId, y.TracingId);
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
                   StringEqual(x.TracingId, y.TracingId);
        }

        private bool LeaveGroupWithAckMessageEqual(LeaveGroupWithAckMessage x, LeaveGroupWithAckMessage y)
        {
            return StringEqual(x.ConnectionId, y.ConnectionId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.AckId == y.AckId &&
                   StringEqual(x.TracingId, y.TracingId);
        }

        private bool CheckUserInGroupWithAckMessageEqual(CheckUserInGroupWithAckMessage x, CheckUserInGroupWithAckMessage y)
        {
            return StringEqual(x.UserId, y.UserId) &&
                   StringEqual(x.GroupName, y.GroupName) &&
                   x.AckId == y.AckId &&
                   StringEqual(x.TracingId, y.TracingId);
        }

        private bool AckMessageEqual(AckMessage x, AckMessage y)
        {
            return x.AckId == y.AckId &&
                   x.Status == y.Status &&
                   StringEqual(x.Message, y.Message);
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
