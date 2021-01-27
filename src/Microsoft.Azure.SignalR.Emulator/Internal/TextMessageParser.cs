// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Microsoft.AspNetCore.Internal
{
    internal static class TextMessageParser
    {
        // This record separator is supposed to be used only for JSON payloads where 0x1e character
        // will not occur (is not a valid character) and therefore it is safe to not escape it
        public static readonly byte RecordSeparator = 0x1e;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseMessage(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> payload)
        {
            if (buffer.IsSingleSegment)
            {
                var span = buffer.First.Span;
                var index = span.IndexOf(RecordSeparator);
                if (index == -1)
                {
                    payload = default;
                    return false;
                }

                payload = buffer.Slice(0, index);

                buffer = buffer.Slice(index + 1);

                return true;
            }
            else
            {
                return TryParseMessageMultiSegment(ref buffer, out payload);
            }
        }

        private static bool TryParseMessageMultiSegment(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> payload)
        {
            var position = buffer.PositionOf(RecordSeparator);
            if (position == null)
            {
                payload = default;
                return false;
            }

            payload = buffer.Slice(0, position.Value);

            // Skip record separator
            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));

            return true;
        }
    }
}