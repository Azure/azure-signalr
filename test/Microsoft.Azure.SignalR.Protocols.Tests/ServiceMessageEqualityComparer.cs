// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

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
                case BroadcastDataMessage broadcastDataMessage:
                    return BroadcastDataMessagesEqual(broadcastDataMessage, (BroadcastDataMessage)y);
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
            return string.Equals(x.ErrorMessage, y.ErrorMessage, StringComparison.Ordinal);
        }

        private bool OpenConnectionMessagesEqual(OpenConnectionMessage x, OpenConnectionMessage y)
        {
            return string.Equals(x.ConnectionId, y.ConnectionId, StringComparison.Ordinal) &&
                   ClaimsEqual(x.Claims, y.Claims);
        }

        private bool CloseConnectionMessagesEqual(CloseConnectionMessage x, CloseConnectionMessage y)
        {
            return string.Equals(x.ConnectionId, y.ConnectionId, StringComparison.Ordinal) &&
                   string.Equals(x.ErrorMessage, y.ErrorMessage);
        }

        private bool ConnectionDataMessagesEqual(ConnectionDataMessage x, ConnectionDataMessage y)
        {
            return string.Equals(x.ConnectionId, y.ConnectionId, StringComparison.Ordinal) &&
                   SequenceEqual(x.Payload.ToArray(), y.Payload.ToArray());
        }

        private bool MultiConnectionDataMessagesEqual(MultiConnectionDataMessage x, MultiConnectionDataMessage y)
        {
            return SequenceEqual(x.ConnectionList, y.ConnectionList) &&
                   PayloadsEqual(x.Payloads, y.Payloads);
        }

        private bool UserDataMessagesEqual(UserDataMessage x, UserDataMessage y)
        {
            return string.Equals(x.UserId, y.UserId, StringComparison.Ordinal) &&
                   PayloadsEqual(x.Payloads, y.Payloads);
        }

        private bool BroadcastDataMessagesEqual(BroadcastDataMessage x, BroadcastDataMessage y)
        {
            return SequenceEqual(x.ExcludedList, y.ExcludedList) &&
                   PayloadsEqual(x.Payloads, y.Payloads);
        }

        private bool ClaimsEqual(Claim[] x, Claim[] y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x.Length != y.Length)
            {
                return false;
            }

            return !x.Where((t, i) => t.Type != y[i].Type || !string.Equals(t.Value, y[i].Value, StringComparison.Ordinal)).Any();
        }

        private bool PayloadsEqual(IDictionary<string, ReadOnlyMemory<byte>> x,
            IDictionary<string, ReadOnlyMemory<byte>> y)
        {
            if (x == null && y == null)
            {
                return true;
            }

            if (x.Count != y.Count)
            {
                return false;
            }

            for (int i = 0; i < x.Count; i++)
            {
                if (!string.Equals(x.ElementAt(i).Key, y.ElementAt(i).Key, StringComparison.Ordinal) ||
                    !SequenceEqual(x.ElementAt(i).Value.ToArray(), y.ElementAt(i).Value.ToArray()))
                {
                    return false;
                }
            }

            return true;
        }

        private bool SequenceEqual(object left, object right)
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
