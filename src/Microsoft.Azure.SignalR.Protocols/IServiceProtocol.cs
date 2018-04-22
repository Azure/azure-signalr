// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;

namespace Microsoft.Azure.SignalR.Protocol
{
    public interface IServiceProtocol
    {
        bool TryParseMessage(ref ReadOnlySequence<byte> input, out ServiceMessage message);

        void WriteMessage(ServiceMessage message, IBufferWriter<byte> output);

        ReadOnlyMemory<byte> GetMessageBytes(ServiceMessage message);
    }
}
