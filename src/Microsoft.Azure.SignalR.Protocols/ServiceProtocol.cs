// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Formatters;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using MsgPack;

namespace Microsoft.Azure.SignalR
{
    public class ServiceProtocol
    {
        public static string ProtocolName = "messagepackwrapper";
        public static readonly int ProtocolVersion = 1;

        public static string Name => ProtocolName;

        public TransferFormat TransferFormat => TransferFormat.Binary;

        public static int Version => ProtocolVersion;

        public bool IsVersionSupported(int version)
        {
            return version == Version;
        }

        public static bool TryParseMessage(ref ReadOnlySequence<byte> input, out HubMessage message)
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

        private static HubMessage ParseMessage(byte[] input, int startOffset)
        {
            using (var unpacker = Unpacker.Create(input, startOffset))
            {
                var len = ReadArrayLength(unpacker, "elementCount");
                var messageType = ReadInt32(unpacker, "messageType");
                switch (messageType)
                {
                    case AzureHubProtocolConstants.ServiceMessageType:
                        return CreateServiceMessage(unpacker, len);
                    case HubProtocolConstants.PingMessageType:
                        return PingMessage.Instance;
                    case HubProtocolConstants.CloseMessageType:
                        return CreateCloseMessage(unpacker);
                    default:
                        throw new FormatException($"Invalid message type: {messageType}.");
                }
            }
        }

        private static ServiceMessage CreateServiceMessage(Unpacker unpacker, long len)
        {
            var format = ReadInt32(unpacker, "messageformat");
            var hubMessageWrapper = new ServiceMessage((TransferFormat)format);
            hubMessageWrapper.InvocationType = (HubInvocationType)ReadInt32(unpacker, "invocationtype");
            var metadata = ReadHeaders(unpacker, "headers");
            hubMessageWrapper.AddMetadata(metadata);
            hubMessageWrapper.JsonPayload = ReadBinary(unpacker, "jsonpayload");
            hubMessageWrapper.MsgpackPayload = ReadBinary(unpacker, "msgpackpayload");
            return hubMessageWrapper;
        }

        private static CloseMessage CreateCloseMessage(Unpacker unpacker)
        {
            var error = ReadString(unpacker, "error");
            return new CloseMessage(error);
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

            throw new FormatException($"Reading '{field}' as String failed.", msgPackException);
        }

        private static IDictionary<string, string> ReadHeaders(Unpacker unpacker, string field)
        {
            unpacker.ReadObject(out var obj);
            if (obj.IsDictionary)
            {
                return obj.AsDictionary().ToDictionary(kvp => kvp.Key.AsString(), kvp => kvp.Value.ToString());
            }
            return new Dictionary<string, string>();
        }

        public static byte[] WriteToArray(HubMessage message)
        {
            var writer = MemoryBufferWriter.Get();
            try
            {
                WriteMessage(message, writer);
                return writer.ToArray();
            }
            finally
            {
                MemoryBufferWriter.Return(writer);
            }
        }

        public static void WriteMessage(HubMessage message, IBufferWriter<byte> output)
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

        private static void WriteMessageCore(HubMessage message, Stream output)
        {
            // PackerCompatibilityOptions.None prevents from serializing byte[] as strings
            // and allows extended objects
            var packer = Packer.Create(output, PackerCompatibilityOptions.None);
            switch (message)
            {
                case ServiceMessage serviceMessage:
                    ServiceMessage(serviceMessage, packer);
                    break;
                case PingMessage pingMessage:
                    WritePingMessage(pingMessage, packer);
                    break;
                case CloseMessage closeMessage:
                    WriteCloseMessage(closeMessage, packer);
                    break;
                default:
                    throw new FormatException($"Unexpected message type: {message.GetType().Name}");
            }
        }

        private static void ServiceMessage(ServiceMessage message, Packer packer)
        {
            packer.PackArrayHeader(6);
            packer.Pack(AzureHubProtocolConstants.ServiceMessageType);
            packer.Pack((int)(message.Format));
            packer.Pack((int)(message.InvocationType));
            packer.PackDictionary<string, string>(message.Headers);
            if (message.JsonPayload != null)
            {
                packer.PackBinary(message.JsonPayload);
            }
            else
            {
                packer.PackNull();
            }
            if (message.MsgpackPayload != null)
            {
                packer.PackBinary(message.MsgpackPayload);
            }
            else
            {
                packer.PackNull();
            }
        }

        private static void WritePingMessage(PingMessage pingMessage, Packer packer)
        {
            packer.PackArrayHeader(1);
            packer.Pack(HubProtocolConstants.PingMessageType);
        }

        private static void WriteCloseMessage(CloseMessage message, Packer packer)
        {
            packer.PackArrayHeader(2);
            packer.Pack(HubProtocolConstants.CloseMessageType);
            if (string.IsNullOrEmpty(message.Error))
            {
                packer.PackNull();
            }
            else
            {
                packer.PackString(message.Error);
            }
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

            throw new FormatException($"Reading array length for '{field}' failed.", msgPackException);
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

            throw new FormatException($"Reading '{field}' as byte[] failed.", msgPackException);
        }
    }
}
