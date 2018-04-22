// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Claims;
using MessagePack;
using Microsoft.AspNetCore.Connections;

namespace Microsoft.Azure.SignalR.Protocol
{
    public class ServiceProtocol : IServiceProtocol
    {
        private static readonly string ProtocolName = "signalrservice";
        private static readonly int ProtocolVersion = 1;

        public string Name => ProtocolName;

        public int Version => ProtocolVersion;

        public TransferFormat TransferFormat => TransferFormat.Binary;

        public bool TryParseMessage(ref ReadOnlySequence<byte> input, out ServiceMessage message)
        {
            if (!BinaryMessageParser.TryParseMessage(ref input, out var payload))
            {
                message = null;
                return false;
            }

            var arraySegment = GetArraySegment(payload);

            message = ParseMessage(arraySegment.Array, arraySegment.Offset);
            return true;
        }

        private static ArraySegment<byte> GetArraySegment(in ReadOnlySequence<byte> input)
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
            _ = MessagePackBinary.ReadArrayHeader(input, startOffset, out var readSize);
            startOffset += readSize;

            var messageType = ReadInt32(input, ref startOffset, "messageType");

            switch (messageType)
            {
                case ServiceProtocolConstants.HandshakeRequestType:
                    return CreateHandshakeRequestMessage(input, ref startOffset);
                case ServiceProtocolConstants.HandshakeResponseType:
                    return CreateHandshakeResponseMessage(input, ref startOffset);
                case ServiceProtocolConstants.PingMessageType:
                    return PingMessage.Instance;
                case ServiceProtocolConstants.OpenConnectionMessageType:
                    return CreateOpenConnectionMessage(input, ref startOffset);
                case ServiceProtocolConstants.CloseConnectionMessageType:
                    return CreateCloseConnectionMessage(input, ref startOffset);
                case ServiceProtocolConstants.ConnectionDataMessageType:
                    return CreateConnectionDataMessage(input, ref startOffset);
                case ServiceProtocolConstants.MultiConnectionDataMessageType:
                    return CreateMultiConnectionDataMessage(input, ref startOffset);
                case ServiceProtocolConstants.UserDataMessageType:
                    return CreateUserDataMessage(input, ref startOffset);
                case ServiceProtocolConstants.MultiUserDataMessageType:
                    return CreateMultiUserDataMessage(input, ref startOffset);
                case ServiceProtocolConstants.BroadcastDataMessageType:
                    return CreateBroadcastDataMessage(input, ref startOffset);
                case ServiceProtocolConstants.JoinGroupMessageType:
                    return CreateJoinGroupMessage(input, ref startOffset);
                case ServiceProtocolConstants.LeaveGroupMessageType:
                    return CreateLeaveGroupMessage(input, ref startOffset);
                case ServiceProtocolConstants.GroupBroadcastDataMessageType:
                    return CreateGroupBroadcastDataMessage(input, ref startOffset);
                case ServiceProtocolConstants.MultiGroupBroadcastDataMessageType:
                    return CreateMultiGroupBroadcastDataMessage(input, ref startOffset);
                default:
                    // Future protocol changes can add message types, old clients can ignore them
                    return null;
            }
        }

        public void WriteMessage(ServiceMessage message, IBufferWriter<byte> output)
        {
            var writer = MemoryBufferWriter.Get();

            try
            {
                // Write message to a buffer so we can get its length
                WriteMessageCore(message, writer);

                // Write length then message to output
                BinaryMessageFormatter.WriteLengthPrefix(writer.Length, output);
                writer.CopyTo(output);
            }
            finally
            {
                MemoryBufferWriter.Return(writer);
            }
        }

        public ReadOnlyMemory<byte> GetMessageBytes(ServiceMessage message)
        {
            var writer = MemoryBufferWriter.Get();

            try
            {
                // Write message to a buffer so we can get its length
                WriteMessageCore(message, writer);

                var dataLength = writer.Length;
                var prefixLength = BinaryMessageFormatter.LengthPrefixLength(writer.Length);

                var array = new byte[dataLength + prefixLength];
                var span = array.AsSpan();

                // Write length then message to output
                var written = BinaryMessageFormatter.WriteLengthPrefix(writer.Length, span);
                Debug.Assert(written == prefixLength);
                writer.CopyTo(span.Slice(prefixLength));

                return array;
            }
            finally
            {
                MemoryBufferWriter.Return(writer);
            }
        }

        private void WriteMessageCore(ServiceMessage message, Stream packer)
        {
            switch (message)
            {
                case HandshakeRequestMessage handshakeRequestMessage:
                    WriteHandshakeRequestMessage(handshakeRequestMessage, packer);
                    break;
                case HandshakeResponseMessage handshakeResponseMessage:
                    WriteHandshakeResponseMessage(handshakeResponseMessage, packer);
                    break;
                case PingMessage pingMessage:
                    WritePingMessage(pingMessage, packer);
                    break;
                case OpenConnectionMessage openConnectionMessage:
                    WriteOpenConnectionMessage(openConnectionMessage, packer);
                    break;
                case CloseConnectionMessage closeConnectionMessage:
                    WriteCloseConnectionMessage(closeConnectionMessage, packer);
                    break;
                case ConnectionDataMessage connectionDataMessage:
                    WriteConnectionDataMessage(connectionDataMessage, packer);
                    break;
                case MultiConnectionDataMessage multiConnectionDataMessage:
                    WriteMultiConnectionDataMessage(multiConnectionDataMessage, packer);
                    break;
                case UserDataMessage userDataMessage:
                    WriteUserDataMessage(userDataMessage, packer);
                    break;
                case MultiUserDataMessage multiUserDataMessage:
                    WriteMultiUserDataMessage(multiUserDataMessage, packer);
                    break;
                case BroadcastDataMessage broadcastDataMessage:
                    WriteBroadcastDataMessage(broadcastDataMessage, packer);
                    break;
                case JoinGroupMessage joinGroupMessage:
                    WriteJoinGroupMessage(joinGroupMessage, packer);
                    break;
                case LeaveGroupMessage leaveGroupMessage:
                    WriteLeaveGroupMessage(leaveGroupMessage, packer);
                    break;
                case GroupBroadcastDataMessage groupBroadcastDataMessage:
                    WriteGroupBroadcastDataMessage(groupBroadcastDataMessage, packer);
                    break;
                case MultiGroupBroadcastDataMessage multiGroupBroadcastDataMessage:
                    WriteMultiGroupBroadcastDataMessage(multiGroupBroadcastDataMessage, packer);
                    break;
                default:
                    throw new InvalidDataException($"Unexpected message type: {message.GetType().Name}");
            }
        }

        private void WriteHandshakeRequestMessage(HandshakeRequestMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 2);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.HandshakeRequestType);
            MessagePackBinary.WriteInt32(packer, message.Version);
        }

        private void WriteHandshakeResponseMessage(HandshakeResponseMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 2);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.HandshakeResponseType);
            MessagePackBinary.WriteString(packer, message.ErrorMessage);
        }

        private void WritePingMessage(PingMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 1);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.PingMessageType);
        }

        private void WriteOpenConnectionMessage(OpenConnectionMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.OpenConnectionMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);

            if (message.Claims?.Any() == true)
            {
                MessagePackBinary.WriteMapHeader(packer, message.Claims.Length);
                foreach (var claim in message.Claims)
                {
                    MessagePackBinary.WriteString(packer, claim.Type);
                    MessagePackBinary.WriteString(packer, claim.Value);
                }
            }
            else
            {
                MessagePackBinary.WriteMapHeader(packer, 0);
            }
        }

        private void WriteCloseConnectionMessage(CloseConnectionMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.CloseConnectionMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.ErrorMessage);
        }

        private void WriteConnectionDataMessage(ConnectionDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.ConnectionDataMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteBytes(packer, message.Payload);
        }

        private void WriteMultiConnectionDataMessage(MultiConnectionDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.MultiConnectionDataMessageType);
            WriteStringArray(message.ConnectionList, packer);
            WritePayloads(message.Payloads, packer);
        }

        private void WriteUserDataMessage(UserDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.UserDataMessageType);
            MessagePackBinary.WriteString(packer, message.UserId);
            WritePayloads(message.Payloads, packer);
        }

        private void WriteMultiUserDataMessage(MultiUserDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.UserDataMessageType);
            WriteStringArray(message.UserList, packer);
            WritePayloads(message.Payloads, packer);
        }

        private void WriteBroadcastDataMessage(BroadcastDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.BroadcastDataMessageType);
            WriteStringArray(message.ExcludedList, packer);
            WritePayloads(message.Payloads, packer);
        }

        private void WriteJoinGroupMessage(JoinGroupMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.JoinGroupMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.GroupName);
        }

        private void WriteLeaveGroupMessage(LeaveGroupMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.LeaveGroupMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.GroupName);
        }

        private void WriteGroupBroadcastDataMessage(GroupBroadcastDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 4);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.GroupBroadcastDataMessageType);
            MessagePackBinary.WriteString(packer, message.GroupName);
            WriteStringArray(message.ExcludedList, packer);
            WritePayloads(message.Payloads, packer);
        }

        private void WriteMultiGroupBroadcastDataMessage(MultiGroupBroadcastDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.MultiGroupBroadcastDataMessageType);
            WriteStringArray(message.GroupList, packer);
            WritePayloads(message.Payloads, packer);
        }

        private void WriteStringArray(string[] array, Stream packer)
        {
            if (array?.Any() == true)
            {
                MessagePackBinary.WriteArrayHeader(packer, array.Length);
                foreach (var value in array)
                {
                    MessagePackBinary.WriteString(packer, value);
                }
            }
            else
            {
                MessagePackBinary.WriteArrayHeader(packer, 0);
            }
        }

        private void WritePayloads(IDictionary<string, byte[]> payloads, Stream packer)
        {
            if (payloads?.Any() == true)
            {
                MessagePackBinary.WriteMapHeader(packer, payloads.Count);
                foreach (var payload in payloads)
                {
                    MessagePackBinary.WriteString(packer, payload.Key);
                    MessagePackBinary.WriteBytes(packer, payload.Value);
                }
            }
            else
            {
                MessagePackBinary.WriteMapHeader(packer, 0);
            }
        }

        private static HandshakeRequestMessage CreateHandshakeRequestMessage(byte[] input, ref int offset)
        {
            var version = ReadInt32(input, ref offset, "version");

            return new HandshakeRequestMessage(version);
        }

        private static HandshakeResponseMessage CreateHandshakeResponseMessage(byte[] input, ref int offset)
        {
            var errorMessage = ReadString(input, ref offset, "errorMessage");

            return new HandshakeResponseMessage(errorMessage);
        }

        private static OpenConnectionMessage CreateOpenConnectionMessage(byte[] input, ref int offset)
        {
            var connectionId = ReadString(input, ref offset, "connectionId");
            var claims = ReadClaims(input, ref offset);

            return new OpenConnectionMessage(connectionId, claims);
        }

        private static CloseConnectionMessage CreateCloseConnectionMessage(byte[] input, ref int offset)
        {
            var connectionId = ReadString(input, ref offset, "connectionId");
            var errorMessage = ReadString(input, ref offset, "errorMessage");

            return new CloseConnectionMessage(connectionId, errorMessage);
        }

        private static ConnectionDataMessage CreateConnectionDataMessage(byte[] input, ref int offset)
        {
            var connectionId = ReadString(input, ref offset, "connectionId");
            var payload = ReadBytes(input, ref offset, "payload");

            return new ConnectionDataMessage(connectionId, payload);
        }

        private static MultiConnectionDataMessage CreateMultiConnectionDataMessage(byte[] input, ref int offset)
        {
            var connectionList = ReadStringArray(input, ref offset, "connectionList");
            var payloads = ReadPayloads(input, ref offset);

            return new MultiConnectionDataMessage(connectionList, payloads);
        }

        private static ServiceMessage CreateUserDataMessage(byte[] input, ref int offset)
        {
            var userId = ReadString(input, ref offset, "userId");
            var payloads = ReadPayloads(input, ref offset);

            return new UserDataMessage(userId, payloads);
        }

        private static MultiUserDataMessage CreateMultiUserDataMessage(byte[] input, ref int offset)
        {
            var userList = ReadStringArray(input, ref offset, "userList");
            var payloads = ReadPayloads(input, ref offset);

            return new MultiUserDataMessage(userList, payloads);
        }

        private static BroadcastDataMessage CreateBroadcastDataMessage(byte[] input, ref int offset)
        {
            var excludedList = ReadStringArray(input, ref offset, "excludedList");
            var payloads = ReadPayloads(input, ref offset);

            return new BroadcastDataMessage(excludedList, payloads);
        }

        private static JoinGroupMessage CreateJoinGroupMessage(byte[] input, ref int offset)
        {
            var connectionId = ReadString(input, ref offset, "connectionId");
            var groupName = ReadString(input, ref offset, "groupName");

            return new JoinGroupMessage(connectionId, groupName);
        }

        private static LeaveGroupMessage CreateLeaveGroupMessage(byte[] input, ref int offset)
        {
            var connectionId = ReadString(input, ref offset, "connectionId");
            var groupName = ReadString(input, ref offset, "groupName");

            return new LeaveGroupMessage(connectionId, groupName);
        }

        private static GroupBroadcastDataMessage CreateGroupBroadcastDataMessage(byte[] input, ref int offset)
        {
            var groupName = ReadString(input, ref offset, "groupName");
            var excludedList = ReadStringArray(input, ref offset, "excludedList");
            var payloads = ReadPayloads(input, ref offset);

            return new GroupBroadcastDataMessage(groupName, excludedList, payloads);
        }

        private static MultiGroupBroadcastDataMessage CreateMultiGroupBroadcastDataMessage(byte[] input, ref int offset)
        {
            var groupList = ReadStringArray(input, ref offset, "groupList");
            var payloads = ReadPayloads(input, ref offset);

            return new MultiGroupBroadcastDataMessage(groupList, payloads);
        }

        private static Claim[] ReadClaims(byte[] input, ref int offset)
        {
            var claimCount = ReadMapLength(input, ref offset, "claims");
            if (claimCount > 0)
            {
                var claims = new Claim[claimCount];

                for (var i = 0; i < claimCount; i++)
                {
                    var type = ReadString(input, ref offset, $"claims[{i}].Type");
                    var value = ReadString(input, ref offset, $"claims[{i}].Value");
                    claims[i] = new Claim(type, value);
                }

                return claims;
            }

            return null;
        }

        private static IDictionary<string, byte[]> ReadPayloads(byte[] input, ref int offset)
        {
            var payloadCount = ReadMapLength(input, ref offset, "payloads");
            if (payloadCount > 0)
            {
                var payloads = new Dictionary<string, byte[]>((int)payloadCount);
                for (var i = 0; i < payloadCount; i++)
                {
                    var key = ReadString(input, ref offset, $"payloads[{i}].key");
                    var value = ReadBytes(input, ref offset, $"payloads[{i}].value");
                    payloads.Add(key, value);
                }

                return payloads;
            }

            return null;
        }

        private static int ReadInt32(byte[] input, ref int offset, string field)
        {
            Exception msgPackException = null;
            try
            {
                var readInt = MessagePackBinary.ReadInt32(input, offset, out var readSize);
                offset += readSize;
                return readInt;
            }
            catch (Exception e)
            {
                msgPackException = e;
            }

            throw new InvalidDataException($"Reading '{field}' as Int32 failed.", msgPackException);
        }

        private static string ReadString(byte[] input, ref int offset, string field)
        {
            Exception msgPackException = null;
            try
            {
                var readString = MessagePackBinary.ReadString(input, offset, out var readSize);
                offset += readSize;
                return readString;
            }
            catch (Exception e)
            {
                msgPackException = e;
            }

            throw new InvalidDataException($"Reading '{field}' as String failed.", msgPackException);
        }

        private static string[] ReadStringArray(byte[] input, ref int offset, string field)
        {
            var arrayLength = ReadArrayLength(input, ref offset, field);
            if (arrayLength > 0)
            {
                var array = new string[arrayLength];
                for (int i = 0; i < arrayLength; i++)
                {
                    array[i] = ReadString(input, ref offset, $"{field}[{i}]");
                }

                return array;
            }

            return null;
        }

        private static byte[] ReadBytes(byte[] input, ref int offset, string field)
        {
            Exception msgPackException = null;
            try
            {
                var readBytes = MessagePackBinary.ReadBytes(input, offset, out var readSize);
                offset += readSize;
                return readBytes;
            }
            catch (Exception e)
            {
                msgPackException = e;
            }

            throw new InvalidDataException($"Reading '{field}' as Byte[] failed.", msgPackException);
        }

        private static long ReadMapLength(byte[] input, ref int offset, string field)
        {
            Exception msgPackException = null;
            try
            {
                var readMap = MessagePackBinary.ReadMapHeader(input, offset, out var readSize);
                offset += readSize;
                return readMap;
            }
            catch (Exception e)
            {
                msgPackException = e;
            }

            throw new InvalidDataException($"Reading map length for '{field}' failed.", msgPackException);
        }

        private static long ReadArrayLength(byte[] input, ref int offset, string field)
        {
            Exception msgPackException = null;
            try
            {
                var readArray = MessagePackBinary.ReadArrayHeader(input, offset, out var readSize);
                offset += readSize;
                return readArray;
            }
            catch (Exception e)
            {
                msgPackException = e;
            }

            throw new InvalidDataException($"Reading array length for '{field}' failed.", msgPackException);
        }
    }
}