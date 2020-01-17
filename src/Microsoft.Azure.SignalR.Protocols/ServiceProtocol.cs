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

            var reader = new MessagePackReader(payload);

            message = ParseMessage(ref reader);
            return true;
        }

        private static ServiceMessage ParseMessage(ref MessagePackReader reader)
        {
            var arrayLength = reader.ReadArrayHeader();

            var messageType = reader.ReadInt32();

            switch (messageType)
            {
                case ServiceProtocolConstants.HandshakeRequestType:
                    return CreateHandshakeRequestMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.HandshakeResponseType:
                    return CreateHandshakeResponseMessage(ref reader);
                case ServiceProtocolConstants.PingMessageType:
                    return CreatePingMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.OpenConnectionMessageType:
                    return CreateOpenConnectionMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.CloseConnectionMessageType:
                    return CreateCloseConnectionMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.ConnectionDataMessageType:
                    return CreateConnectionDataMessage(ref reader);
                case ServiceProtocolConstants.MultiConnectionDataMessageType:
                    return CreateMultiConnectionDataMessage(ref reader);
                case ServiceProtocolConstants.UserDataMessageType:
                    return CreateUserDataMessage(ref reader);
                case ServiceProtocolConstants.MultiUserDataMessageType:
                    return CreateMultiUserDataMessage(ref reader);
                case ServiceProtocolConstants.BroadcastDataMessageType:
                    return CreateBroadcastDataMessage(ref reader);
                case ServiceProtocolConstants.JoinGroupMessageType:
                    return CreateJoinGroupMessage(ref reader);
                case ServiceProtocolConstants.LeaveGroupMessageType:
                    return CreateLeaveGroupMessage(ref reader);
                case ServiceProtocolConstants.UserJoinGroupMessageType:
                    return CreateUserJoinGroupMessage(ref reader);
                case ServiceProtocolConstants.UserLeaveGroupMessageType:
                    return CreateUserLeaveGroupMessage(ref reader);
                case ServiceProtocolConstants.GroupBroadcastDataMessageType:
                    return CreateGroupBroadcastDataMessage(ref reader);
                case ServiceProtocolConstants.MultiGroupBroadcastDataMessageType:
                    return CreateMultiGroupBroadcastDataMessage(ref reader);
                case ServiceProtocolConstants.ServiceErrorMessageType:
                    return CreateServiceErrorMessage(ref reader);
                case ServiceProtocolConstants.JoinGroupWithAckMessageType:
                    return CreateJoinGroupWithAckMessage(ref reader);
                case ServiceProtocolConstants.LeaveGroupWithAckMessageType:
                    return CreateLeaveGroupWithAckMessage(ref reader);
                case ServiceProtocolConstants.AckMessageType:
                    return CreateAckMessage(ref reader);
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
                case JoinGroupWithAckMessage joinGroupWithAckMessage:
                    WriteJoinGroupWithAckMessage(joinGroupWithAckMessage, packer);
                    break;
                case LeaveGroupMessage leaveGroupMessage:
                    WriteLeaveGroupMessage(leaveGroupMessage, packer);
                    break;
                case LeaveGroupWithAckMessage leaveGroupWithAckMessage:
                    WriteLeaveGroupWithAckMessage(leaveGroupWithAckMessage, packer);
                    break;
                case UserJoinGroupMessage userJoinGroupMessage:
                    WriteUserJoinGroupMessage(userJoinGroupMessage, packer);
                    break;
                case UserLeaveGroupMessage userLeaveGroupMessage:
                    WriteUserLeaveGroupMessage(userLeaveGroupMessage, packer);
                    break;
                case GroupBroadcastDataMessage groupBroadcastDataMessage:
                    WriteGroupBroadcastDataMessage(groupBroadcastDataMessage, packer);
                    break;
                case MultiGroupBroadcastDataMessage multiGroupBroadcastDataMessage:
                    WriteMultiGroupBroadcastDataMessage(multiGroupBroadcastDataMessage, packer);
                    break;
                case ServiceErrorMessage serviceErrorMessage:
                    WriteServiceErrorMessage(serviceErrorMessage, packer);
                    break;
                case AckMessage ackMessage:
                    WriteAckMessage(ackMessage, packer);
                    break;
                default:
                    throw new InvalidDataException($"Unexpected message type: {message.GetType().Name}");
            }
        }

        private static void WriteHandshakeRequestMessage(HandshakeRequestMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 5);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.HandshakeRequestType);
            MessagePackBinary.WriteInt32(packer, message.Version);
            MessagePackBinary.WriteInt32(packer, message.ConnectionType);
            MessagePackBinary.WriteString(packer, message.ConnectionType == 0 ? "" : message.Target ?? string.Empty);
            MessagePackBinary.WriteInt32(packer, (int)message.MigrationLevel);
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
            MessagePackBinary.WriteArrayHeader(packer, 4);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.CloseConnectionMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.ErrorMessage);
            WriteHeaders(message.Headers, packer);
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

        private static void WriteUserJoinGroupMessage(UserJoinGroupMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.UserJoinGroupMessageType);
            MessagePackBinary.WriteString(packer, message.UserId);
            MessagePackBinary.WriteString(packer, message.GroupName);
        }

        private static void WriteUserLeaveGroupMessage(UserLeaveGroupMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 3);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.UserLeaveGroupMessageType);
            MessagePackBinary.WriteString(packer, message.UserId);
            MessagePackBinary.WriteString(packer, message.GroupName);
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

        private static void WriteServiceErrorMessage(ServiceErrorMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 2);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.ServiceErrorMessageType);
            MessagePackBinary.WriteString(packer, message.ErrorMessage);
        }

        private static void WriteJoinGroupWithAckMessage(JoinGroupWithAckMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 4);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.JoinGroupWithAckMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.GroupName);
            MessagePackBinary.WriteInt32(packer, message.AckId);
        }

        private static void WriteLeaveGroupWithAckMessage(LeaveGroupWithAckMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 4);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.LeaveGroupWithAckMessageType);
            MessagePackBinary.WriteString(packer, message.ConnectionId);
            MessagePackBinary.WriteString(packer, message.GroupName);
            MessagePackBinary.WriteInt32(packer, message.AckId);
        }

        private static void WriteAckMessage(AckMessage message, Stream packer)
        {
            MessagePackBinary.WriteArrayHeader(packer, 4);
            MessagePackBinary.WriteInt32(packer, ServiceProtocolConstants.AckMessageType);
            MessagePackBinary.WriteInt32(packer, message.AckId);
            MessagePackBinary.WriteInt32(packer, message.Status);
            MessagePackBinary.WriteString(packer, message.Message);
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

        private static HandshakeRequestMessage CreateHandshakeRequestMessage(ref MessagePackReader reader, int arrayLength)
        {
            var version = ReadInt32(ref reader, "version");
            var result = new HandshakeRequestMessage(version);
            if (arrayLength >= 4)
            {
                result.ConnectionType = ReadInt32(ref reader, "connectionType");
                result.Target = ReadString(ref reader, "target");
            }
            result.MigrationLevel = arrayLength >= 5 ? ReadInt32(ref reader, "migratableStatus") : 0;
            return result;
        }

        private static HandshakeResponseMessage CreateHandshakeResponseMessage(ref MessagePackReader reader)
        {
            var errorMessage = ReadString(ref reader, "errorMessage");

            return new HandshakeResponseMessage(errorMessage);
        }

        private static PingMessage CreatePingMessage(ref MessagePackReader reader, int arrayLength)
        {
            if (arrayLength > 1)
            {
                var length = arrayLength - 1;
                var values = new string[length];
                for (int i = 0; i < length; i++)
                {
                    values[i] = ReadString(ref reader, $"messages[{i}]");
                }

                return new PingMessage { Messages = values };
            }
            return PingMessage.Instance;
        }

        private static OpenConnectionMessage CreateOpenConnectionMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var claims = ReadClaims(ref reader);

            // Backward compatible with old versions
            if (arrayLength > 3)
            {
                var headers = ReadHeaders(ref reader);
                var queryString = ReadString(ref reader, "queryString");
                return new OpenConnectionMessage(connectionId, claims, headers, queryString);
            }
            else
            {
                return new OpenConnectionMessage(connectionId, claims);
            }
        }

        private static CloseConnectionMessage CreateCloseConnectionMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var errorMessage = ReadString(ref reader, "errorMessage");
            var headers = arrayLength > 3 ? ReadHeaders(ref reader) : new Dictionary<string, StringValues>();
            return new CloseConnectionMessage(connectionId, errorMessage, headers);
        }

        private static ConnectionDataMessage CreateConnectionDataMessage(ref MessagePackReader reader)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var payload = ReadBytes(ref reader, "payload");

            return new ConnectionDataMessage(connectionId, payload);
        }

        private static MultiConnectionDataMessage CreateMultiConnectionDataMessage(ref MessagePackReader reader)
        {
            var connectionList = ReadStringArray(ref reader, "connectionList");
            var payloads = ReadPayloads(ref reader);

            return new MultiConnectionDataMessage(connectionList, payloads);
        }

        private static ServiceMessage CreateUserDataMessage(ref MessagePackReader reader)
        {
            var userId = ReadString(ref reader, "userId");
            var payloads = ReadPayloads(ref reader);

            return new UserDataMessage(userId, payloads);
        }

        private static MultiUserDataMessage CreateMultiUserDataMessage(ref MessagePackReader reader)
        {
            var userList = ReadStringArray(ref reader, "userList");
            var payloads = ReadPayloads(ref reader);

            return new MultiUserDataMessage(userList, payloads);
        }

        private static BroadcastDataMessage CreateBroadcastDataMessage(ref MessagePackReader reader)
        {
            var excludedList = ReadStringArray(ref reader, "excludedList");
            var payloads = ReadPayloads(ref reader);

            return new BroadcastDataMessage(excludedList, payloads);
        }

        private static JoinGroupMessage CreateJoinGroupMessage(ref MessagePackReader reader)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var groupName = ReadString(ref reader, "groupName");

            return new JoinGroupMessage(connectionId, groupName);
        }

        private static LeaveGroupMessage CreateLeaveGroupMessage(ref MessagePackReader reader)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var groupName = ReadString(ref reader, "groupName");

            return new LeaveGroupMessage(connectionId, groupName);
        }

        private static UserJoinGroupMessage CreateUserJoinGroupMessage(ref MessagePackReader reader)
        {
            var userId = ReadString(ref reader, "userId");
            var groupName = ReadString(ref reader, "groupName");

            return new UserJoinGroupMessage(userId, groupName);
        }

        private static UserLeaveGroupMessage CreateUserLeaveGroupMessage(ref MessagePackReader reader)
        {
            var userId = ReadString(ref reader, "userId");
            var groupName = ReadString(ref reader, "groupName");

            return new UserLeaveGroupMessage(userId, groupName);
        }

        private static GroupBroadcastDataMessage CreateGroupBroadcastDataMessage(ref MessagePackReader reader)
        {
            var groupName = ReadString(ref reader, "groupName");
            var excludedList = ReadStringArray(ref reader, "excludedList");
            var payloads = ReadPayloads(ref reader);

            return new GroupBroadcastDataMessage(groupName, excludedList, payloads);
        }

        private static MultiGroupBroadcastDataMessage CreateMultiGroupBroadcastDataMessage(ref MessagePackReader reader)
        {
            var groupList = ReadStringArray(ref reader, "groupList");
            var payloads = ReadPayloads(ref reader);

            return new MultiGroupBroadcastDataMessage(groupList, payloads);
        }

        private static ServiceErrorMessage CreateServiceErrorMessage(ref MessagePackReader reader)
        {
            var errorMessage = ReadString(ref reader, "errorMessage");

            return new ServiceErrorMessage(errorMessage);
        }

        private static JoinGroupWithAckMessage CreateJoinGroupWithAckMessage(ref MessagePackReader reader)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var groupName = ReadString(ref reader, "groupName");
            var ackId = ReadInt32(ref reader, "ackId");

            return new JoinGroupWithAckMessage(connectionId, groupName, ackId);
        }

        private static LeaveGroupWithAckMessage CreateLeaveGroupWithAckMessage(ref MessagePackReader reader)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var groupName = ReadString(ref reader, "groupName");
            var ackId = ReadInt32(ref reader, "ackId");

            return new LeaveGroupWithAckMessage(connectionId, groupName, ackId);
        }

        private static AckMessage CreateAckMessage(ref MessagePackReader reader)
        {
            var ackId = ReadInt32(ref reader, "ackId");
            var status = ReadInt32(ref reader, "status");
            var message = ReadString(ref reader, "message");

            return new AckMessage(ackId, status, message);
        }

        private static Claim[] ReadClaims(ref MessagePackReader reader)
        {
            var claimCount = ReadMapLength(ref reader, "claims");
            if (claimCount > 0)
            {
                var claims = new Claim[claimCount];

                for (var i = 0; i < claimCount; i++)
                {
                    var type = ReadString(ref reader, $"claims[{i}].Type");
                    var value = ReadString(ref reader, $"claims[{i}].Value");
                    claims[i] = new Claim(type, value);
                }

                return claims;
            }

            return null;
        }

        private static IDictionary<string, ReadOnlyMemory<byte>> ReadPayloads(ref MessagePackReader reader)
        {
            var payloadCount = ReadMapLength(ref reader, "payloads");
            if (payloadCount > 0)
            {
                var payloads = new Dictionary<string, ReadOnlyMemory<byte>>((int)payloadCount);
                for (var i = 0; i < payloadCount; i++)
                {
                    var key = ReadString(ref reader, $"payloads[{i}].key");
                    var value = ReadBytes(ref reader, $"payloads[{i}].value");
                    payloads.Add(key, value);
                }

                return payloads;
            }

            return null;
        }

        private static Dictionary<string, StringValues> ReadHeaders(ref MessagePackReader reader)
        {
            var headerCount = ReadMapLength(ref reader, "headers");
            if (headerCount > 0)
            {
                var headers = new Dictionary<string, StringValues>((int)headerCount, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < headerCount; i++)
                {
                    var key = ReadString(ref reader, $"headers[{i}].key");
                    var count = ReadArrayLength(ref reader, $"headers[{i}].value.length");
                    var stringValues = new string[count];
                    for (var j = 0; j < count; j++)
                    {
                        stringValues[j] = ReadString(ref reader, $"headers[{i}].value[{j}]");
                    }
                    headers.Add(key, stringValues);
                }

                return headers;
            }

            return new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);
        }

        private static int ReadInt32(ref MessagePackReader reader, string field)
        {
            try
            {
                return reader.ReadInt32();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Reading '{field}' as Int32 failed.", ex);
            }

        }

        private static string ReadString(ref MessagePackReader reader, string field)
        {
            try
            {
                return reader.ReadString();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Reading '{field}' as String failed.", ex);
            }
        }

        private static string[] ReadStringArray(ref MessagePackReader reader, string field)
        {
            var arrayLength = ReadArrayLength(ref reader, field);
            if (arrayLength > 0)
            {
                var array = new string[arrayLength];
                for (int i = 0; i < arrayLength; i++)
                {
                    array[i] = ReadString(ref reader, $"{field}[{i}]");
                }

                return array;
            }

            return null;
        }

        private static byte[] ReadBytes(ref MessagePackReader reader, string field)
        {
            try
            {
                return reader.ReadBytes()?.ToArray() ?? Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Reading '{field}' as Byte[] failed.", ex);
            }

        }

        private static long ReadMapLength(ref MessagePackReader reader, string field)
        {
            try
            {
                return reader.ReadMapHeader();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Reading map length for '{field}' failed.", ex);
            }
        }

        private static long ReadArrayLength(ref MessagePackReader reader, string field)
        {
            try
            {
                return reader.ReadArrayHeader();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Reading array length for '{field}' failed.", ex);
            }

        }
    }
}
