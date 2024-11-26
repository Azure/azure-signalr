// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System;
using System.Buffers;

namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// A protocol abstraction for communication between Azure SignalR Service and SDK.
    /// </summary>
    public interface IServiceProtocol
    {
        /// <summary>
        /// Gets the version of the protocol.
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Creates a new <see cref="ServiceMessage"/> from the specified serialized representation.
        /// </summary>
        /// <param name="input">The serialized representation of the message.</param>
        /// <param name="message">When this method returns <c>true</c>, contains the parsed message.</param>
        /// <returns>A value that is <c>true</c> if the <see cref="ServiceMessage"/> was successfully parsed; otherwise, <c>false</c>.</returns>
        bool TryParseMessage(ref ReadOnlySequence<byte> input, out ServiceMessage? message);

        /// <summary>
        /// Writes the specified <see cref="ServiceMessage"/> to a writer.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <param name="output">The output writer.</param>
        void WriteMessage(ServiceMessage message, IBufferWriter<byte> output);

        /// <summary>
        /// Converts the specified <see cref="ServiceMessage"/> to its serialized representation.
        /// </summary>
        /// <param name="message">The message to convert.</param>
        /// <returns>The serialized representation of the message.</returns>
        ReadOnlyMemory<byte> GetMessageBytes(ServiceMessage message);
    }
}
