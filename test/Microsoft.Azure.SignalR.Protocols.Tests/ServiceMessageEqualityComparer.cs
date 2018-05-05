// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;

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
                case OpenConnectionMessage openConnectionMessage:
                    return OpenConnectionMessagesEqual(openConnectionMessage, (OpenConnectionMessage)y);
                case PingMessage _:
                    return true;
                default:
                    throw new InvalidOperationException($"Unknown message type: {x.GetType().FullName}");
            }
        }

        public int GetHashCode(ServiceMessage obj)
        {
            return 0;
        }

        private bool OpenConnectionMessagesEqual(OpenConnectionMessage x, OpenConnectionMessage y)
        {
            return string.Equals(x.ConnectionId, y.ConnectionId, StringComparison.Ordinal) &&
                   SequenceEqual(x.Claims, y.Claims);
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
