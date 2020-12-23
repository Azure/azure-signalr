// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal class LimitedStream : Stream
    {
        private readonly MemoryStream _ms = new MemoryStream();
        private readonly int _maxSize;

        public LimitedStream(int maxSize)
        {
            _maxSize = maxSize;
        }

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => true;

        public override long Length => _ms.Length;

        public override long Position { get => _ms.Position; set => _ms.Position = value; }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            _ms.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            _ms.Seek(offset, origin);

        public override void SetLength(long value)
        {
            if (value > _maxSize)
            {
                throw new InvalidDataException("Exceed size limit.");
            }
            _ms.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _ms.Write(buffer, offset, count);
            if (_ms.Length > _maxSize)
            {
                throw new InvalidDataException("Exceed size limit.");
            }
        }

        public ReadOnlyMemory<byte> ToMemory() =>
            new ReadOnlyMemory<byte>(_ms.GetBuffer(), 0, (int)_ms.Length);
    }
}
