// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using MsgPack;
using MsgPack.Serialization;

namespace Microsoft.Azure.SignalR
{
    public class ServiceProtocol
    {
        internal static SerializationContext DefaultSerializationContext = new SerializationContext
        {
            // serializes objects (here: arguments and results) as maps so that property names are preserved
            SerializationMethod = SerializationMethod.Map,
            // allows for serializing objects that cannot be deserialized due to the lack of the default ctor etc.
            CompatibilityOptions =
            {
                AllowAsymmetricSerializer = true
            }
        };

        public static readonly string Name = "ServiceProtocol";
        
        public SerializationContext SerializationContext { get; } = DefaultSerializationContext;

        public static int Version => 1;

        public static bool IsVersionSupported(int version)
        {
            return version == Version;
        }

        public static bool TryParseMessage(ref ReadOnlySequence<byte> input, out ServiceMessage message)
        {
            if (!BinaryMessageParser.TryParseMessage(ref input, out var payload))
            {
                message = null;
                return false;
            }

            var arraySegment = GetArraySegment(payload);

            message = ParseMessage(arraySegment.Array, arraySegment.Offset);

            return message != null;
        }

        private static ArraySegment<byte> GetArraySegment(ReadOnlySequence<byte> input)
        {
            if (input.IsSingleSegment)
            {
                var isArray = MemoryMarshal.TryGetArray(input.First, out var arraySegment);
                // This will never be false unless we started using un-managed buffers
                Debug.Assert(isArray);
                return arraySegment;
            }

            // Should be rare
            return new ArraySegment<byte>(input.ToArray());
        }

        private static ServiceMessage ParseMessage(byte[] input, int startOffset)
        {
            using (var unpacker = Unpacker.Create(input, startOffset))
            {
                _ = ReadArrayLength(unpacker, "elementCount");

                var command = ReadCommand(unpacker);
                var arguments = ReadArguments(unpacker);

                var payloads = ReadPayloads(unpacker);

                return new ServiceMessage
                {
                    Command = command,
                    Arguments = arguments,
                    Payloads = payloads
                };
            }
        }

        private static CommandType ReadCommand(Unpacker unpacker)
        {
            return (CommandType)ReadInt32(unpacker, "command");
        }

        private static IDictionary<ArgumentType, string> ReadArguments(Unpacker unpacker)
        {
            var argumentCount = ReadMapLength(unpacker, "arguments");
            if (argumentCount <= 0)
            {
                return null;
            }

            var arguments = new Dictionary<ArgumentType, string>((int)argumentCount);

            for (var i = 0; i < argumentCount; i++)
            {
                var key = ReadString(unpacker, $"arguments[{i}].Key");
                var value = ReadString(unpacker, $"arguments[{i}].Value");
                if (Enum.TryParse<ArgumentType>(key, out var argumentType))
                {
                    arguments[argumentType] = value;
                }
            }
            return arguments;
        }

        private static IDictionary<string, byte[]> ReadPayloads(Unpacker unpacker)
        {
            var payloadCount = ReadMapLength(unpacker, "payloads");
            if (payloadCount <= 0)
            {
                return null;
            }

            var payloads = new Dictionary<string, byte[]>((int)payloadCount);

            for (var i = 0; i < payloadCount; i++)
            {
                var key = ReadString(unpacker, $"payloads[{i}].Key");
                var value = ReadBinary(unpacker, $"payloads[{i}].Value");
                payloads[key] = value;
            }
            return payloads;
        }

        public static byte[] WriteToArray(ServiceMessage message)
        {
            var writer = MemoryBufferWriter.Get();
            try
            {
                ServiceProtocol.WriteMessage(message, writer);
                return writer.ToArray();
            }
            finally
            {
                MemoryBufferWriter.Return(writer);
            }
        }

        public static void WriteMessage(ServiceMessage message, IBufferWriter<byte> output)
        {
            using (var stream = new LimitArrayPoolWriteStream())
            {
                // Write message to a buffer so we can get its length
                WriteMessageCore(message, stream);
                var buffer = stream.GetBuffer();

                // Write length then message to output
                BinaryMessageFormatter.WriteLengthPrefix(buffer.Count, output);
                output.Write(buffer);
            }
        }

        private static void WriteMessageCore(ServiceMessage message, Stream output)
        {
            // PackerCompatibilityOptions.None prevents from serializing byte[] as strings
            // and allows extended objects
            var packer = Packer.Create(output, PackerCompatibilityOptions.None);
            packer.PackArrayHeader(3);
            packer.Pack((int)(message.Command));
            PackArguments(packer, message.Arguments);
            PackPayloads(packer, message.Payloads);
        }

        private static void PackArguments(Packer packer, IDictionary<ArgumentType, string> arguments)
        {
            if (arguments != null && arguments.Count > 0)
            {
                packer.PackMapHeader(arguments.Count);
                foreach (var argument in arguments)
                {
                    packer.Pack(argument.Key);
                    packer.PackString(argument.Value);
                }
            }
            else
            {
                packer.PackMapHeader(0);
            }
        }

        private static void PackPayloads(Packer packer, IDictionary<string, byte[]> payloads)
        {
            if (payloads != null && payloads.Count > 0)
            {
                packer.PackMapHeader(payloads.Count);
                foreach (var payload in payloads)
                {
                    packer.PackString(payload.Key);
                    packer.PackBinary(payload.Value);
                }
            }
            else
            {
                packer.PackMapHeader(0);
            }
        }

        private static int ReadInt32(Unpacker unpacker, string field)
        {
            Exception msgPackException = null;
            try
            {
                if (unpacker.ReadInt32(out var value))
                {
                    return value;
                }
            }
            catch (Exception e)
            {
                msgPackException = e;
            }

            throw new FormatException($"Reading '{field}' as Int32 failed.", msgPackException);
        }

        private static string ReadString(Unpacker unpacker, string field)
        {
            Exception msgPackException = null;
            try
            {
                if (unpacker.Read())
                {
                    if (unpacker.LastReadData.IsNil)
                    {
                        return null;
                    }
                    else
                    {
                        return unpacker.LastReadData.AsString();
                    }
                }
            }
            catch (Exception e)
            {
                msgPackException = e;
            }

            throw new InvalidDataException($"Reading '{field}' as String failed.", msgPackException);
        }

        private static long ReadMapLength(Unpacker unpacker, string field)
        {
            Exception msgPackException = null;
            try
            {
                if (unpacker.ReadMapLength(out var value))
                {
                    return value;
                }
            }
            catch (Exception e)
            {
                msgPackException = e;
            }

            throw new InvalidDataException($"Reading map length for '{field}' failed.", msgPackException);
        }

        private static long ReadArrayLength(Unpacker unpacker, string field)
        {
            Exception msgPackException = null;
            try
            {
                if (unpacker.ReadArrayLength(out var value))
                {
                    return value;
                }
            }
            catch (Exception e)
            {
                msgPackException = e;
            }

            throw new InvalidDataException($"Reading array length for '{field}' failed.", msgPackException);
        }

        private static byte[] ReadBinary(Unpacker unpacker, string field)
        {
            Exception msgPackException = null;
            try
            {
                if (unpacker.Read())
                {
                    if (unpacker.LastReadData.IsNil)
                    {
                        return null;
                    }
                    else
                    {
                        return unpacker.LastReadData.AsBinary();
                    }
                }
            }
            catch (Exception e)
            {
                msgPackException = e;
            }

            throw new InvalidDataException($"Reading '{field}' as byte[] failed.", msgPackException);
        }
    }
}
