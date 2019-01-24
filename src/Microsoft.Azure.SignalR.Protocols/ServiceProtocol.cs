// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Claims;
using MessagePack;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// Implements the Azure SignalR Service Protocol.
    /// </summary>
    public class ServiceProtocol : IServiceProtocol
    {
        private static readonly int ProtocolVersion = 1;

        /// <inheritdoc />
        public int Version => ProtocolVersion;

        /// <inheritdoc />
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
            var arrayLength = MessagePackBinary.ReadArrayHeader(input, startOffset, out var readSize);
            startOffset += readSize;

            var messageType = ReadInt32(input, ref startOffset, "messageType");

            switch (messageType)
            {
                case ServiceProtocolConstants.HandshakeRequestType:
                    return CreateHandshakeRequestMessage(input, ref startOffset, arrayLength);
                case ServiceProtocolConstants.HandshakeResponseType:
                    return CreateHandshakeResponseMessage(input, ref startOffset);
                case ServiceProtocolConstants.PingMessageType:
                    return CreatePingMessage(input, ref startOffset, arrayLength);
                case ServiceProtocolConstants.OpenConnectionMessageType:
                    return CreateOpenConnectionMessage(arrayLength, input, ref startOffset);
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
                case ServiceProtocolConstants.ServiceErrorMessageType:
                    return CreateServiceErrorMessage(input, ref startOffset);
                case ServiceProtocolConstants.JoinGroupWithAckMessageType:
                    return CreateJoinGroupWithAckMessage(input, ref startOffset);
                case ServiceProtocolConstants.LeaveGroupWithAckMessageType:
                    return CreateLeaveGroupWithAckMessage(input, ref startOffset);
                case ServiceProtocolConstants.GroupBroadcastDataWithAckMessageType:
                    return CreateGroupBroadcastDataWithAckMessage(input, ref startOffset);
                case ServiceProtocolConstants.MultiGroupBroadcastDataWithAckMessageType:
                    return CreateMultiGroupBroadcastDataWithAckMessage(input, ref startOffset);
                case ServiceProtocolConstants.GroupAckMessageType:
                    return CreateGroupAckMessage(input, ref startOffset);
                default:
                    // Future protocol changes can add message types, old clients can ignore them
                    return null;
            }
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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

        private static void WriteMessageCore(ServiceMessage message, Stream packer)
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
                case GroupBroadcastDataWithAckMessage groupBroadcastDataWithAckMessage:
                    WriteGroupBroadcastDataWithAckMessage(groupBroadcastDataWithAckMessage, packer);
                    break;
                case MultiGroupBroadcastDataWithAckMessage multiGroupBroadcastDataWithAckMessage:
                    WriteMultiGroupBroadcastDataWithAckMessage(multiGroupBroadcastDataWithAckMessage, packer);
                    break;
                case ServiceErrorMessage serviceErrorMessage:
                    WriteServiceErrorMessage(serviceErrorMessage, packer);
                    break;
                case JoinGroupWithAckMessage joinGroupWithAckMessage:
                    WriteJoinGroupWithAckMessage(joinGroupWithAckMessage, packer);
                    break;
                case LeaveGroupWithAckMessage leaveGroupWithAckMessage:
                    WriteLeaveGroupWithAckMessage(leaveGroupWithAckMessage, packer);
                    break;
                case GroupAckMessage groupAckMessage:
                    WriteGroupAckMessage(groupAckMessage, packer);
                    break;
                default:
                    throw new InvalidDataException($"Unexpected message type: {message.GetType().Name}");
            }
        }

        private static void WriteHandshakeRequestMessage(HandshakeRequestMessage message, Stream packer)
        {
            if (message.ConnectionType == 0)
            {
                MessagePackBinary.WriteArrayHeader(packer, 2);
                MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.HandshakeRequestType);
                MessagePackBinary.WriteInt32(packer, message.Version);
            }
            else
            {
                MessagePackBinary.WriteArrayHeader(packer, 4);
                MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.HandshakeRequestType);
                MessagePackBinary.WriteInt32(packer, message.Version);
                MessagePackBinary.WriteInt32(packer, message.ConnectionType);
                MessagePackBinary.WriteString(packer, message.Target ?? string.Empty);
            }
        }

        private static void WriteHandshakeResponseMessage(HandshakeResponseMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 2);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.HandshakeResponseType);
            MessagePackBinary.WriteString(packer, message.ErrorMessage);
        }

        private static void WritePingMessage(PingMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, message.Messages.Length + 1);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.PingMessageType);
            foreach (var item in message.Messages)
            {
                MessagePackBinary.WriteString(packer, item);
            }
        }

        private static void WriteOpenConnectionMessage(OpenConnectionMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 5);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.OpenConnectionMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);

            if (message.Claims?.Length > 0)
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

            WriteHeaders(message.Headers, packer);

            MessagePackBinary.WriteString(packer, message.QueryString);
        }

        private static void WriteCloseConnectionMessage(CloseConnectionMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.CloseConnectionMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.ErrorMessage);
        }

        private static void WriteConnectionDataMessage(ConnectionDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.ConnectionDataMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            WriteBinary(packer, message.Payload);
        }

        private static void WriteBinary(Stream packer, ReadOnlySequence<byte> payload)
        {
            // We're manually writing the message pack binary payload to the stream directly
            // because MessagePack-Csharp doesn't support writing the binary header outside of 
            // calling WriteBytes directly
            var count = (int)payload.Length;
            if (count <= byte.MaxValue)
            {
                packer.WriteByte(MessagePackCode.Bin8);
                packer.WriteByte((byte)count);
            }
            else if (count <= UInt16.MaxValue)
            {
                packer.WriteByte(MessagePackCode.Bin16);
                packer.WriteByte((byte)(count >> 8));
                packer.WriteByte((byte)count);
            }
            else
            {
                packer.WriteByte(MessagePackCode.Bin32);
                packer.WriteByte((byte)(count >> 24));
                packer.WriteByte((byte)(count >> 16));
                packer.WriteByte((byte)(count >> 8));
                packer.WriteByte((byte)count);
            }

            // Now writes the raw bytes to the stream directly
            var position = payload.Start;
            while (payload.TryGet(ref position, out var memory))
            {
                bool isArray = MemoryMarshal.TryGetArray(memory, out var segment);
                Debug.Assert(isArray, "We're not using managed memory");
                packer.Write(segment.Array, segment.Offset, segment.Count);
            }
        }

        private static void WriteMultiConnectionDataMessage(MultiConnectionDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.MultiConnectionDataMessageType);
            WriteStringArray(message.ConnectionList, packer);
            WritePayloads(message.Payloads, packer);
        }

        private static void WriteUserDataMessage(UserDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.UserDataMessageType);
            MessagePackBinary.WriteString(packer, message.UserId);
            WritePayloads(message.Payloads, packer);
        }

        private static void WriteMultiUserDataMessage(MultiUserDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.MultiUserDataMessageType);
            WriteStringArray(message.UserList, packer);
            WritePayloads(message.Payloads, packer);
        }

        private static void WriteBroadcastDataMessage(BroadcastDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.BroadcastDataMessageType);
            WriteStringArray(message.ExcludedList, packer);
            WritePayloads(message.Payloads, packer);
        }

        private static void WriteJoinGroupMessage(JoinGroupMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.JoinGroupMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.GroupName);
        }

        private static void WriteLeaveGroupMessage(LeaveGroupMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.LeaveGroupMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.GroupName);
        }

        private static void WriteJoinGroupWithAckMessage(JoinGroupWithAckMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 4);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.JoinGroupWithAckMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.GroupName);
            MessagePackBinary.WriteString(packer, message.AckGuid);
        }

        private static void WriteLeaveGroupWithAckMessage(LeaveGroupWithAckMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 4);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.LeaveGroupWithAckMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.GroupName);
            MessagePackBinary.WriteString(packer, message.AckGuid);
        }

        private static void WriteGroupAckMessage(GroupAckMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 2);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.GroupAckMessageType);
            MessagePackBinary.WriteString(packer, message.AckGuid);
        }

        private static void WriteGroupBroadcastDataMessage(GroupBroadcastDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 4);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.GroupBroadcastDataMessageType);
            MessagePackBinary.WriteString(packer, message.GroupName);
            WriteStringArray(message.ExcludedList, packer);
            WritePayloads(message.Payloads, packer);
        }

        private static void WriteMultiGroupBroadcastDataMessage(MultiGroupBroadcastDataMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.MultiGroupBroadcastDataMessageType);
            WriteStringArray(message.GroupList, packer);
            WritePayloads(message.Payloads, packer);
        }

        private static void WriteGroupBroadcastDataWithAckMessage(GroupBroadcastDataWithAckMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 5);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.GroupBroadcastDataWithAckMessageType);
            MessagePackBinary.WriteString(packer, message.GroupName);
            WriteStringArray(message.ExcludedList, packer);
            MessagePackBinary.WriteString(packer, message.AckGuid);
            WritePayloads(message.Payloads, packer);
        }

        private static void WriteMultiGroupBroadcastDataWithAckMessage(MultiGroupBroadcastDataWithAckMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 4);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.MultiGroupBroadcastDataWithAckMessageType);
            WriteStringArray(message.GroupList, packer);
            MessagePackBinary.WriteString(packer, message.AckGuid);
            WritePayloads(message.Payloads, packer);
        }

        private static void WriteServiceErrorMessage(ServiceErrorMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 2);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.ServiceErrorMessageType);
            MessagePackBinary.WriteString(packer, message.ErrorMessage);
        }

        private static void WriteStringArray(IReadOnlyList<string> array, Stream packer)
        {
            if (array?.Count > 0)
            {
                MessagePackBinary.WriteArrayHeader(packer, array.Count);
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

        private static void WritePayloads(IDictionary<string, ReadOnlyMemory<byte>> payloads, Stream packer)
        {
            if (payloads?.Count > 0)
            {
                MessagePackBinary.WriteMapHeader(packer, payloads.Count);
                foreach (var payload in payloads)
                {
                    MessagePackBinary.WriteString(packer, payload.Key);
                    bool isArray = MemoryMarshal.TryGetArray(payload.Value, out var segment);
                    Debug.Assert(isArray, "We're not using managed memory");
                    MessagePackBinary.WriteBytes(packer, segment.Array, segment.Offset, segment.Count);
                }
            }
            else
            {
                MessagePackBinary.WriteMapHeader(packer, 0);
            }
        }

        private static void WriteHeaders(IDictionary<string, StringValues> headers, Stream packer)
        {
            if (headers?.Count > 0)
            {
                MessagePackBinary.WriteMapHeader(packer, headers.Count);
                foreach (var header in headers)
                {
                    MessagePackBinary.WriteString(packer, header.Key);
                    MessagePackBinary.WriteArrayHeader(packer, header.Value.Count);
                    foreach (var stringValue in header.Value)
                    {
                        MessagePackBinary.WriteString(packer, stringValue);
                    }
                }
            }
            else
            {
                MessagePackBinary.WriteMapHeader(packer, 0);
            }
        }

        private static HandshakeRequestMessage CreateHandshakeRequestMessage(byte[] input, ref int offset, int arrayLength)
        {
            var version = ReadInt32(input, ref offset, "version");
            var result = new HandshakeRequestMessage(version);
            if (arrayLength >= 4)
            {
                result.ConnectionType = ReadInt32(input, ref offset, "connectionType");
                result.Target = ReadString(input, ref offset, "target");
            }
            return result;
        }

        private static HandshakeResponseMessage CreateHandshakeResponseMessage(byte[] input, ref int offset)
        {
            var errorMessage = ReadString(input, ref offset, "errorMessage");

            return new HandshakeResponseMessage(errorMessage);
        }

        private static PingMessage CreatePingMessage(byte[] input, ref int offset, int arrayLength)
        {
            if (arrayLength > 1)
            {
                var length = arrayLength - 1;
                var values = new string[length];
                for (int i = 0; i < length; i++)
                {
                    values[i] = ReadString(input, ref offset, $"messages[{i}]");
                }

                return new PingMessage { Messages = values };
            }
            return PingMessage.Instance;
        }

        private static OpenConnectionMessage CreateOpenConnectionMessage(int arrayLength, byte[] input, ref int offset)
        {
            var connectionId = ReadString(input, ref offset, "connectionId");
            var claims = ReadClaims(input, ref offset);

            // Backward compatible with old versions
            if (arrayLength > 3)
            {
                var headers = ReadHeaders(input, ref offset);
                var queryString = ReadString(input, ref offset, "queryString");

                return new OpenConnectionMessage(connectionId, claims, headers, queryString);
            }
            else
            {
                return new OpenConnectionMessage(connectionId, claims);
            }
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

        private static JoinGroupWithAckMessage CreateJoinGroupWithAckMessage(byte[] input, ref int offset)
        {
            var connectionId = ReadString(input, ref offset, "connectionId");
            var groupName = ReadString(input, ref offset, "groupName");
            var ackGuid = ReadString(input, ref offset, "ackGuid");

            return new JoinGroupWithAckMessage(connectionId, groupName, ackGuid);
        }

        private static LeaveGroupWithAckMessage CreateLeaveGroupWithAckMessage(byte[] input, ref int offset)
        {
            var connectionId = ReadString(input, ref offset, "connectionId");
            var groupName = ReadString(input, ref offset, "groupName");
            var ackGuid = ReadString(input, ref offset, "ackGuid");

            return new LeaveGroupWithAckMessage(connectionId, groupName, ackGuid);
        }

        private static GroupAckMessage CreateGroupAckMessage(byte[] input, ref int offset)
        {
            var ackGuid = ReadString(input, ref offset, "ackGuid");

            return new GroupAckMessage(ackGuid);
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

        private static GroupBroadcastDataWithAckMessage CreateGroupBroadcastDataWithAckMessage(byte[] input, ref int offset)
        {
            var groupName = ReadString(input, ref offset, "groupName");
            var excludedList = ReadStringArray(input, ref offset, "excludedList");
            var ackGuid = ReadString(input, ref offset, "ackGuid");
            var payloads = ReadPayloads(input, ref offset);

            return new GroupBroadcastDataWithAckMessage(groupName, excludedList, payloads, ackGuid);
        }

        private static MultiGroupBroadcastDataWithAckMessage CreateMultiGroupBroadcastDataWithAckMessage(byte[] input, ref int offset)
        {
            var groupList = ReadStringArray(input, ref offset, "groupList");
            var ackGuid = ReadString(input, ref offset, "ackGuid");
            var payloads = ReadPayloads(input, ref offset);

            return new MultiGroupBroadcastDataWithAckMessage(groupList, payloads, ackGuid);
        }

        private static ServiceErrorMessage CreateServiceErrorMessage(byte[] input, ref int offset)
        {
            var errorMessage = ReadString(input, ref offset, "errorMessage");

            return new ServiceErrorMessage(errorMessage);
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

        private static IDictionary<string, ReadOnlyMemory<byte>> ReadPayloads(byte[] input, ref int offset)
        {
            var payloadCount = ReadMapLength(input, ref offset, "payloads");
            if (payloadCount > 0)
            {
                var payloads = new Dictionary<string, ReadOnlyMemory<byte>>((int)payloadCount);
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

        private static Dictionary<string, StringValues> ReadHeaders(byte[] input, ref int offset)
        {
            var headerCount = ReadMapLength(input, ref offset, "headers");
            if (headerCount > 0)
            {
                var headers = new Dictionary<string, StringValues>((int)headerCount, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headerCount; i++)
                {
                    var key = ReadString(input, ref offset, $"headers[{i}].key");
                    var count = ReadArrayLength(input, ref offset, $"headers[{i}].value.length");
                    var stringValues = new string[count];
                    for (var j = 0; j < count; j++)
                    {
                        stringValues[j] = ReadString(input, ref offset, $"headers[{i}].value[{j}]");
                    }
                    headers.Add(key, stringValues);
                }

                return headers;
            }

            return new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
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
