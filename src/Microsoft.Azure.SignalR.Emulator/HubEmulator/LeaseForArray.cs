// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal static class LeaseForArray
    {
        public static LeaseForArray<T> Create<T>(T[] array, int count) =>
            new LeaseForArray<T>(new ArraySegment<T>(array, 0, count));

        public static LeaseForArray<T> Create<T>(T[] array, int index, int count) =>
            new LeaseForArray<T>(new ArraySegment<T>(array, index, count));
    }

    internal struct LeaseForArray<T> : IDisposable
    {
        public static readonly LeaseForArray<T> Empty = new LeaseForArray<T>(new ArraySegment<T>(Array.Empty<T>()));

        public LeaseForArray(ArraySegment<T> value)
        {
            Value = value;
        }

        public ArraySegment<T> Value { get; }

        public void Dispose()
        {
            if (Value.Array == Array.Empty<T>())
            {
                return;
            }
            ArrayPool<T>.Shared.Return(Value.Array);
        }
    }
}
