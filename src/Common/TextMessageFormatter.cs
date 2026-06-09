// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace Microsoft.Azure.SignalR
{    
     /// <summary>
     /// Copied from https://github.com/dotnet/aspnetcore/blob/d9660d157627af710b71c636fa8cb139616cadba/src/SignalR/common/Shared/TextMessageFormatter.cs
     /// </summary>
    internal static class TextMessageFormatter
    {
        // This record separator is supposed to be used only for JSON payloads where 0x1e character
        // will not occur (is not a valid character) and therefore it is safe to not escape it
        public const byte RecordSeparator = 0x1e;

        public static void WriteRecordSeparator(IBufferWriter<byte> output)
        {
            var buffer = output.GetSpan(1);
            buffer[0] = RecordSeparator;
            output.Advance(1);
        }
    }
}