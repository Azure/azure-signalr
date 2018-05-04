// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;
using Xunit;

namespace Microsoft.Azure.SignalR.Protocol.Tests
{
    public class ServiceProtocolFacts
    {
        private static readonly IServiceProtocol ServiceProtocol = new ServiceProtocol();

        [Fact]
        public void ParseMessages()
        {
            // TODO
        }

        [Fact]
        public void WriteMessages()
        {
            // TODO
        }

        [Fact]
        public void ParseMessageWithExtraData()
        {
            var expectedMessage = new OpenConnectionMessage("id", null);

            // Verify that the input binary string decodes to the expected MsgPack primitives
            var bytes = new byte[] { ArrayBytes(3), 4, StringBytes(2), (byte)'i', (byte)'d', MapBytes(0), StringBytes(2), (byte)'e', (byte)'x' };

            // Parse the input fully now.
            bytes = Frame(bytes);
            var data = new ReadOnlySequence<byte>(bytes);
            Assert.True(ServiceProtocol.TryParseMessage(ref data, out var message));

            Assert.NotNull(message);
            Assert.Equal(expectedMessage, message, ServiceMessageEqualityComparer.Instance);
        }

        private static byte ArrayBytes(int size)
        {
            return (byte) (0x90 | size);
        }

        private static byte StringBytes(int size)
        {
            return (byte) (0xa0 | size);
        }

        private static byte MapBytes(int size)
        {
            return (byte) (0x80 | size);
        }

        private static byte[] Frame(byte[] input)
        {
            var stream = MemoryBufferWriter.Get();
            try
            {
                BinaryMessageFormatter.WriteLengthPrefix(input.Length, stream);
                stream.Write(input);
                return stream.ToArray();
            }
            finally
            {
                MemoryBufferWriter.Return(stream);
            }
        }
    }
}
