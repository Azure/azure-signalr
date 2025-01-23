// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;

namespace Microsoft.Azure.SignalR.Common
{
    internal class ExactSizeMemoryPool : MemoryPool<byte>
    {
        public static new ExactSizeMemoryPool Shared { get; } = new ExactSizeMemoryPool();

        public override int MaxBufferSize => int.MaxValue;

        public override IMemoryOwner<byte> Rent(int size)
        {
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size, 0);
#else
            if (size <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(size));
            }
#endif
            return new ExactSizeMemoryOwner(size);
        }

        protected override void Dispose(bool disposing)
        {
        }

        private sealed class ExactSizeMemoryOwner : IMemoryOwner<byte>
        {
            private readonly int _size;
            private byte[] _array;

            public ExactSizeMemoryOwner(int size)
            {
                _size = size;
                _array = ArrayPool<byte>.Shared.Rent(size);
            }

            public Memory<byte> Memory
            {
                get
                {
                    var array = _array;
#if NET8_0_OR_GREATER
                    ObjectDisposedException.ThrowIf(array == null, typeof(IMemoryOwner<byte>));
#else
                    if (array == null)
                    {
                        throw new ObjectDisposedException(nameof(IMemoryOwner<byte>));
                    }
#endif
                    return new Memory<byte>(array, 0, _size);
                }
            }

            public void Dispose()
            {
                var array = _array;
                if (array != null)
                {
                    _array = null;
                    ArrayPool<byte>.Shared.Return(array);
                }
            }
        }
    }
}
