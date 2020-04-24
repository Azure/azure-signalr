// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
                    return CreateBroadcastDataMessage(ref reader, arrayLength);
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
            var memoryBufferWriter = MemoryBufferWriter.Get();

            try
            {
                var writer = new MessagePackWriter(memoryBufferWriter);

                // Write message to a buffer so we can get its length
                WriteMessageCore(ref writer, message);

                // Write length then message to output
                BinaryMessageFormatter.WriteLengthPrefix(memoryBufferWriter.Length, output);
                memoryBufferWriter.CopyTo(output);
            }
            finally
            {
                MemoryBufferWriter.Return(memoryBufferWriter);
            }
        }

        /// <inheritdoc />
        public ReadOnlyMemory<byte> GetMessageBytes(ServiceMessage message)
        {
            var memoryBufferWriter = MemoryBufferWriter.Get();

            try
            {
                var writer = new MessagePackWriter(memoryBufferWriter);

                // Write message to a buffer so we can get its length
                WriteMessageCore(ref writer, message);

                var dataLength = memoryBufferWriter.Length;
                var prefixLength = BinaryMessageFormatter.LengthPrefixLength(memoryBufferWriter.Length);

                var array = new byte[dataLength + prefixLength];
                var span = array.AsSpan();

                // Write length then message to output
                var written = BinaryMessageFormatter.WriteLengthPrefix(memoryBufferWriter.Length, span);
                Debug.Assert(written == prefixLength);
                memoryBufferWriter.CopyTo(span.Slice(prefixLength));

                return array;
            }
            finally
            {
                MemoryBufferWriter.Return(memoryBufferWriter);
            }
        }

        private static void WriteMessageCore(ref MessagePackWriter writer, ServiceMessage message)
        {
            switch (message)
            {
                case HandshakeRequestMessage handshakeRequestMessage:
                    WriteHandshakeRequestMessage(ref writer, handshakeRequestMessage);
                    break;
                case HandshakeResponseMessage handshakeResponseMessage:
                    WriteHandshakeResponseMessage(ref writer, handshakeResponseMessage);
                    break;
                case PingMessage pingMessage:
                    WritePingMessage(ref writer, pingMessage);
                    break;
                case OpenConnectionMessage openConnectionMessage:
                    WriteOpenConnectionMessage(ref writer, openConnectionMessage);
                    break;
                case CloseConnectionMessage closeConnectionMessage:
                    WriteCloseConnectionMessage(ref writer, closeConnectionMessage);
                    break;
                case ConnectionDataMessage connectionDataMessage:
                    WriteConnectionDataMessage(ref writer, connectionDataMessage);
                    break;
                case MultiConnectionDataMessage multiConnectionDataMessage:
                    WriteMultiConnectionDataMessage(ref writer, multiConnectionDataMessage);
                    break;
                case UserDataMessage userDataMessage:
                    WriteUserDataMessage(ref writer, userDataMessage);
                    break;
                case MultiUserDataMessage multiUserDataMessage:
                    WriteMultiUserDataMessage(ref writer, multiUserDataMessage);
                    break;
                case BroadcastDataMessage broadcastDataMessage:
                    WriteBroadcastDataMessage(ref writer, broadcastDataMessage);
                    break;
                case JoinGroupMessage joinGroupMessage:
                    WriteJoinGroupMessage(ref writer, joinGroupMessage);
                    break;
                case JoinGroupWithAckMessage joinGroupWithAckMessage:
                    WriteJoinGroupWithAckMessage(ref writer, joinGroupWithAckMessage);
                    break;
                case LeaveGroupMessage leaveGroupMessage:
                    WriteLeaveGroupMessage(ref writer, leaveGroupMessage);
                    break;
                case LeaveGroupWithAckMessage leaveGroupWithAckMessage:
                    WriteLeaveGroupWithAckMessage(ref writer, leaveGroupWithAckMessage);
                    break;
                case UserJoinGroupMessage userJoinGroupMessage:
                    WriteUserJoinGroupMessage(ref writer, userJoinGroupMessage);
                    break;
                case UserLeaveGroupMessage userLeaveGroupMessage:
                    WriteUserLeaveGroupMessage(ref writer, userLeaveGroupMessage);
                    break;
                case GroupBroadcastDataMessage groupBroadcastDataMessage:
                    WriteGroupBroadcastDataMessage(ref writer, groupBroadcastDataMessage);
                    break;
                case MultiGroupBroadcastDataMessage multiGroupBroadcastDataMessage:
                    WriteMultiGroupBroadcastDataMessage(ref writer, multiGroupBroadcastDataMessage);
                    break;
                case ServiceErrorMessage serviceErrorMessage:
                    WriteServiceErrorMessage(ref writer, serviceErrorMessage);
                    break;
                case AckMessage ackMessage:
                    WriteAckMessage(ref writer, ackMessage);
                    break;
                default:
                    throw new InvalidDataException($"Unexpected message type: {message.GetType().Name}");
            }

            writer.Flush();
        }

        private static void WriteHandshakeRequestMessage(ref MessagePackWriter writer, HandshakeRequestMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.HandshakeRequestType);
            writer.Write(message.Version);
            writer.Write(message.ConnectionType);
            writer.Write(message.ConnectionType == 0 ? "" : message.Target ?? string.Empty);
            writer.Write((int)message.MigrationLevel);
        }

        private static void WriteHandshakeResponseMessage(ref MessagePackWriter writer, HandshakeResponseMessage message)
        {
            writer.WriteArrayHeader(2);
            writer.Write(ServiceProtocolConstants.HandshakeResponseType);
            writer.Write(message.ErrorMessage);
        }

        private static void WritePingMessage(ref MessagePackWriter writer, PingMessage message)
        {
            writer.WriteArrayHeader(message.Messages.Length + 1);
            writer.Write(ServiceProtocolConstants.PingMessageType);
            foreach (var item in message.Messages)
            {
                writer.Write(item);
            }
        }

        private static void WriteOpenConnectionMessage(ref MessagePackWriter writer, OpenConnectionMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.OpenConnectionMessageType);
            writer.Write(message.ConnectionId);

            if (message.Claims?.Length > 0)
            {
                writer.WriteMapHeader(message.Claims.Length);
                foreach (var claim in message.Claims)
                {
                    writer.Write(claim.Type);
                    writer.Write(claim.Value);
                }
            }
            else
            {
                writer.WriteMapHeader(0);
            }
            WriteHeaders(ref writer, message.Headers);

            writer.Write(message.QueryString);
        }

        private static void WriteCloseConnectionMessage(ref MessagePackWriter writer, CloseConnectionMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.CloseConnectionMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.ErrorMessage);
            WriteHeaders(ref writer, message.Headers);
        }

        private static void WriteConnectionDataMessage(ref MessagePackWriter writer, ConnectionDataMessage message)
        {
            writer.WriteArrayHeader(3);
            writer.Write(ServiceProtocolConstants.ConnectionDataMessageType);
            writer.Write(message.ConnectionId);

            /************ REVIEW ************/
            // REVIEW : PREVIOUS CODE WAS writing every bytes manualy, not sure if this is the strict equivalent in term of serialization
            writer.Write(message.Payload);
        }

        private static void WriteMultiConnectionDataMessage(ref MessagePackWriter writer, MultiConnectionDataMessage message)
        {
            writer.WriteArrayHeader(3);
            writer.Write(ServiceProtocolConstants.MultiConnectionDataMessageType);
            WriteStringArray(ref writer, message.ConnectionList);
            WritePayloads(ref writer, message.Payloads);
        }

        private static void WriteUserDataMessage(ref MessagePackWriter writer, UserDataMessage message)
        {
            writer.WriteArrayHeader(3);
            writer.Write(ServiceProtocolConstants.UserDataMessageType);
            writer.Write(message.UserId);
            WritePayloads(ref writer, message.Payloads);
        }

        private static void WriteMultiUserDataMessage(ref MessagePackWriter writer, MultiUserDataMessage message)
        {
            writer.WriteArrayHeader(3);
            writer.Write(ServiceProtocolConstants.MultiUserDataMessageType);
            WriteStringArray(ref writer, message.UserList);
            WritePayloads(ref writer, message.Payloads);
        }

        private static void WriteBroadcastDataMessage(ref MessagePackWriter writer, BroadcastDataMessage message)
        {
            var arrayLength = message.MessageId != null ? 4 : 3;
            writer.WriteArrayHeader(arrayLength);
            writer.Write(ServiceProtocolConstants.BroadcastDataMessageType);
            WriteStringArray(ref writer, message.ExcludedList);
            WritePayloads(ref writer, message.Payloads);

            if (message.MessageId != null)
            {
                writer.Write(message.MessageId);
            }
        }

        private static void WriteJoinGroupMessage(ref MessagePackWriter writer, JoinGroupMessage message)
        {
            writer.WriteArrayHeader(3);
            writer.Write(ServiceProtocolConstants.JoinGroupMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.GroupName);
        }

        private static void WriteLeaveGroupMessage(ref MessagePackWriter writer, LeaveGroupMessage message)
        {
            writer.WriteArrayHeader(3);
            writer.Write(ServiceProtocolConstants.LeaveGroupMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.GroupName);
        }

        private static void WriteUserJoinGroupMessage(ref MessagePackWriter writer, UserJoinGroupMessage message)
        {
            writer.WriteArrayHeader(3);
            writer.Write(ServiceProtocolConstants.UserJoinGroupMessageType);
            writer.Write(message.UserId);
            writer.Write(message.GroupName);
        }

        private static void WriteUserLeaveGroupMessage(ref MessagePackWriter writer, UserLeaveGroupMessage message)
        {
            writer.WriteArrayHeader(3);
            writer.Write(ServiceProtocolConstants.UserLeaveGroupMessageType);
            writer.Write(message.UserId);
            writer.Write(message.GroupName);
        }

        private static void WriteGroupBroadcastDataMessage(ref MessagePackWriter writer, GroupBroadcastDataMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.GroupBroadcastDataMessageType);
            writer.Write(message.GroupName);
            WriteStringArray(ref writer, message.ExcludedList);
            WritePayloads(ref writer, message.Payloads);
        }

        private static void WriteMultiGroupBroadcastDataMessage(ref MessagePackWriter writer, MultiGroupBroadcastDataMessage message)
        {
            writer.WriteArrayHeader(3);
            writer.Write(ServiceProtocolConstants.MultiGroupBroadcastDataMessageType);
            WriteStringArray(ref writer, message.GroupList);
            WritePayloads(ref writer, message.Payloads);
        }

        private static void WriteServiceErrorMessage(ref MessagePackWriter writer, ServiceErrorMessage message)
        {
            writer.WriteArrayHeader(2);
            writer.Write(ServiceProtocolConstants.ServiceErrorMessageType);
            writer.Write(message.ErrorMessage);
        }

        private static void WriteJoinGroupWithAckMessage(ref MessagePackWriter writer, JoinGroupWithAckMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.JoinGroupWithAckMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.GroupName);
            writer.Write(message.AckId);
        }

        private static void WriteLeaveGroupWithAckMessage(ref MessagePackWriter writer, LeaveGroupWithAckMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.LeaveGroupWithAckMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.GroupName);
            writer.Write(message.AckId);
        }

        private static void WriteAckMessage(ref MessagePackWriter writer, AckMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.AckMessageType);
            writer.Write(message.AckId);
            writer.Write(message.Status);
            writer.Write(message.Message);
        }

        private static void WriteStringArray(ref MessagePackWriter writer, IReadOnlyList<string> array)
        {
            if (array?.Count > 0)
            {
                writer.WriteArrayHeader(array.Count);
                foreach (var value in array)
                {
                    writer.Write(value);
                }
            }
            else
            {
                writer.WriteArrayHeader(0);
            }
        }

        private static void WritePayloads(ref MessagePackWriter writer, IDictionary<string, ReadOnlyMemory<byte>> payloads)
        {
            if (payloads?.Count > 0)
            {
                writer.WriteMapHeader(payloads.Count);
                foreach (var payload in payloads)
                {
                    writer.Write(payload.Key);

                    /*********************************************************************************/
                    writer.Write(payload.Value.Span);
                    /*********************************************************************************/
                    // REVIEW : PREVIOUS CODE WAS :
                    //bool isArray = MemoryMarshal.TryGetArray(payload.Value, out var segment);
                    //Debug.Assert(isArray, "We're not using managed memory");

                    // writer.WriteBytes(segment.Array, segment.Offset, segment.Count);
                    /*********************************************************************************/
                }
            }
            else
            {
                writer.WriteMapHeader(0);
            }
        }

        private static void WriteHeaders(ref MessagePackWriter writer, IDictionary<string, StringValues> headers)
        {
            if (headers?.Count > 0)
            {
                writer.WriteMapHeader(headers.Count);
                foreach (var header in headers)
                {
                    writer.Write(header.Key);
                    writer.WriteArrayHeader(header.Value.Count);
                    foreach (var stringValue in header.Value)
                    {
                        writer.Write(stringValue);
                    }
                }
            }
            else
            {
                writer.WriteMapHeader(0);
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

        private static BroadcastDataMessage CreateBroadcastDataMessage(ref MessagePackReader reader, int arrayLength)
        {
            var excludedList = ReadStringArray(ref reader, "excludedList");
            var payloads = ReadPayloads(ref reader);
            string messageId = null;
            if (arrayLength == 4)
            {
                messageId = ReadString(ref reader, "messageId");
            }

            return new BroadcastDataMessage(messageId, excludedList, payloads);
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
