// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private static readonly IDictionary<string, ReadOnlyMemory<byte>> EmptyReadOnlyMemoryDictionary = new Dictionary<string, ReadOnlyMemory<byte>>();

        private static readonly IDictionary<string, StringValues> EmptyStringValuesDictionaryIgnoreCase = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

        private static readonly int ProtocolVersion = 1;

        /// <inheritdoc />
        public int Version => ProtocolVersion;

        /// <inheritdoc />
        public bool TryParseMessage(ref ReadOnlySequence<byte> input, out ServiceMessage? message)
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

        private static ServiceMessage? ParseMessage(ref MessagePackReader reader)
        {
            var arrayLength = reader.ReadArrayHeader();

            var messageType = reader.ReadInt32();

            switch (messageType)
            {
                case ServiceProtocolConstants.HandshakeRequestType:
                    return CreateHandshakeRequestMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.HandshakeResponseType:
                    return CreateHandshakeResponseMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.AccessKeyRequestType:
                    return CreateAccessKeyRequestMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.AccessKeyResponseType:
                    return CreateAccessKeyResponseMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.PingMessageType:
                    return CreatePingMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.OpenConnectionMessageType:
                    return CreateOpenConnectionMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.CloseConnectionMessageType:
                    return CreateCloseConnectionMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.ConnectionDataMessageType:
                    return CreateConnectionDataMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.ConnectionReconnectMessageType:
                    return CreateConnectionReconnectMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.MultiConnectionDataMessageType:
                    return CreateMultiConnectionDataMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.UserDataMessageType:
                    return CreateUserDataMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.MultiUserDataMessageType:
                    return CreateMultiUserDataMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.BroadcastDataMessageType:
                    return CreateBroadcastDataMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.JoinGroupMessageType:
                    return CreateJoinGroupMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.LeaveGroupMessageType:
                    return CreateLeaveGroupMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.UserJoinGroupMessageType:
                    return CreateUserJoinGroupMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.UserLeaveGroupMessageType:
                    return CreateUserLeaveGroupMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.UserJoinGroupWithAckMessageType:
                    return CreateUserJoinGroupWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.UserLeaveGroupWithAckMessageType:
                    return CreateUserLeaveGroupWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.GroupBroadcastDataMessageType:
                    return CreateGroupBroadcastDataMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.MultiGroupBroadcastDataMessageType:
                    return CreateMultiGroupBroadcastDataMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.ServiceErrorMessageType:
                    return CreateServiceErrorMessage(ref reader);
                case ServiceProtocolConstants.ServiceEventMessageType:
                    return CreateServiceEventMessage(ref reader);
                case ServiceProtocolConstants.JoinGroupWithAckMessageType:
                    return CreateJoinGroupWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.LeaveGroupWithAckMessageType:
                    return CreateLeaveGroupWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.CheckUserInGroupWithAckMessageType:
                    return CreateCheckUserInGroupWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.CheckGroupExistenceWithAckMessageType:
                    return CreateGroupExistenceWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.CheckConnectionExistenceWithAckMessageType:
                    return CreateCheckConnectionExistenceWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.CheckUserExistenceWithAckMessageType:
                    return CreateCheckUserExistenceWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.CloseConnectionsWithAckMessageType:
#pragma warning disable CS0612 // Type or member is obsolete
                    return CreateCloseConnectionsWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.CloseConnectionWithAckMessageType:
                    return CreateCloseConnectionWithAckMessage(ref reader, arrayLength);
#pragma warning restore CS0612 // Type or member is obsolete
                case ServiceProtocolConstants.CloseUserConnectionsWithAckMessageType:
                    return CreateCloseUserConnectionsWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.CloseGroupConnectionsWithAckMessageType:
                    return CreateCloseGroupConnectionsWithAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.AckMessageType:
                    return CreateAckMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.ClientInvocationMessageType:
                    return CreateClientInvocationMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.ClientCompletionMessageType:
                    return CreateClientCompletionMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.ErrorCompletionMessageType:
                    return CreateErrorCompletionMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.ServiceMappingMessageType:
                    return CreateServiceMappingMessage(ref reader, arrayLength);
                case ServiceProtocolConstants.ConnectionFlowControlMessageType:
                    return CreateConnectionFlowControlMessage(ref reader, arrayLength);
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
#pragma warning disable CS0618 // Type or member is obsolete
            switch (message)
            {
                case HandshakeRequestMessage handshakeRequestMessage:
                    WriteHandshakeRequestMessage(ref writer, handshakeRequestMessage);
                    break;
                case HandshakeResponseMessage handshakeResponseMessage:
                    WriteHandshakeResponseMessage(ref writer, handshakeResponseMessage);
                    break;
                case AccessKeyRequestMessage accessKeyRequestMessage:
                    WriteAccessKeyRequestMessage(ref writer, accessKeyRequestMessage);
                    break;
                case AccessKeyResponseMessage accessKeyResponseMessage:
                    WriteAccessKeyResponseMessage(ref writer, accessKeyResponseMessage);
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
                case ConnectionReconnectMessage connectionReconnectMessage:
                    WriteConnectionReconnectMessage(ref writer, connectionReconnectMessage);
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
                case CheckUserInGroupWithAckMessage checkUserInGroupWithAckMessage:
                    WriteCheckUserInGroupWithAckMessage(ref writer, checkUserInGroupWithAckMessage);
                    break;
                case CheckGroupExistenceWithAckMessage checkAnyConnectionInGroupWithAckMessage:
                    WriteCheckGroupExistenceWithAckMessage(ref writer, checkAnyConnectionInGroupWithAckMessage);
                    break;
                case CheckConnectionExistenceWithAckMessage checkConnectionExistenceWithAckMessage:
                    WriteCheckConnectionExistenceWithAckMessage(ref writer, checkConnectionExistenceWithAckMessage);
                    break;
                case CheckUserExistenceWithAckMessage checkConnectionExistenceAsUserWithAckMessage:
                    WriteCheckUserExistenceWithAckMessage(ref writer, checkConnectionExistenceAsUserWithAckMessage);
                    break;
                case UserJoinGroupMessage userJoinGroupMessage:
                    WriteUserJoinGroupMessage(ref writer, userJoinGroupMessage);
                    break;
                case UserLeaveGroupMessage userLeaveGroupMessage:
                    WriteUserLeaveGroupMessage(ref writer, userLeaveGroupMessage);
                    break;
                case UserJoinGroupWithAckMessage userJoinGroupWithAckMessage:
                    WriteUserJoinGroupWithAckMessage(ref writer, userJoinGroupWithAckMessage);
                    break;
                case UserLeaveGroupWithAckMessage userLeaveGroupWithAckMessage:
                    WriteUserLeaveGroupWithAckMessage(ref writer, userLeaveGroupWithAckMessage);
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
                case ServiceEventMessage serviceWarningMessage:
                    WriteServiceEventMessage(ref writer, serviceWarningMessage);
                    break;
                case CloseConnectionWithAckMessage closeConnectionWithAckMessage:
#pragma warning disable CS0612 // Type or member is obsolete
                    WriteCloseConnectionWithAckMessage(ref writer, closeConnectionWithAckMessage);
                    break;
                case CloseConnectionsWithAckMessage closeConnectionsWithAckMessage:
                    WriteCloseConnectionsWithAckMessage(ref writer, closeConnectionsWithAckMessage);
#pragma warning restore CS0612 // Type or member is obsolete
                    break;
                case CloseUserConnectionsWithAckMessage closeUserConnectionsWithAckMessage:
                    WriteCloseUserConnectionsWithAckMessage(ref writer, closeUserConnectionsWithAckMessage);
                    break;
                case CloseGroupConnectionsWithAckMessage closeGroupConnectionsWithAckMessage:
                    WriteCloseGroupConnectionsWithAckMessage(ref writer, closeGroupConnectionsWithAckMessage);
                    break;
                case AckMessage ackMessage:
                    WriteAckMessage(ref writer, ackMessage);
                    break;
                case ClientInvocationMessage clientInvocationMessage:
                    WriteClientInvocationMessage(ref writer, clientInvocationMessage);
                    break;
                case ClientCompletionMessage clientCompletionMesssage:
                    WriteClientCompletionMessage(ref writer, clientCompletionMesssage);
                    break;
                case ErrorCompletionMessage errorCompletionMesssage:
                    WriteErrorCompletionMessage(ref writer, errorCompletionMesssage);
                    break;
                case ServiceMappingMessage serviceMappingMessage:
                    WriteServiceMappingMessage(ref writer, serviceMappingMessage);
                    break;
                case ConnectionFlowControlMessage connectionFlowControlMessage:
                    WriteConnectionFlowControlMessage(ref writer, connectionFlowControlMessage);
                    break;
                default:
                    throw new InvalidDataException($"Unexpected message type: {message.GetType().Name}");
            }
#pragma warning restore CS0618 // Type or member is obsolete

            writer.Flush();
        }

        private static void WriteHandshakeRequestMessage(ref MessagePackWriter writer, HandshakeRequestMessage message)
        {
            writer.WriteArrayHeader(7);
            writer.Write(ServiceProtocolConstants.HandshakeRequestType);
            writer.Write(message.Version);
            writer.Write(message.ConnectionType);
            writer.Write(message.ConnectionType == 0 ? "" : message.Target ?? string.Empty);
            writer.Write(message.MigrationLevel);
            message.WriteExtensionMembers(ref writer);
            writer.Write(message.AllowStatefulReconnects);
        }

        private static void WriteHandshakeResponseMessage(ref MessagePackWriter writer, HandshakeResponseMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.HandshakeResponseType);
            writer.Write(message.ErrorMessage);
            message.WriteExtensionMembers(ref writer);
            writer.Write(message.ConnectionId);
        }

        private static void WriteAccessKeyRequestMessage(ref MessagePackWriter writer, AccessKeyRequestMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.AccessKeyRequestType);
            writer.Write(message.Token);
            writer.Write(message.Kid);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteAccessKeyResponseMessage(ref MessagePackWriter writer, AccessKeyResponseMessage message)
        {
            writer.WriteArrayHeader(6);
            writer.Write(ServiceProtocolConstants.AccessKeyResponseType);
            writer.Write(message.Kid);
            writer.Write(message.AccessKey);
            writer.Write(message.ErrorType);
            writer.Write(message.ErrorMessage);
            message.WriteExtensionMembers(ref writer);
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
            writer.WriteArrayHeader(6);
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
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteCloseConnectionMessage(ref MessagePackWriter writer, CloseConnectionMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.CloseConnectionMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.ErrorMessage);
            WriteHeaders(ref writer, message.Headers);
            message.WriteExtensionMembers(ref writer);
        }

        [Obsolete]
        private static void WriteCloseConnectionWithAckMessage(ref MessagePackWriter writer, CloseConnectionWithAckMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.CloseConnectionWithAckMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.Reason);
            writer.Write(message.AckId);
            message.WriteExtensionMembers(ref writer);
        }

        [Obsolete]
        private static void WriteCloseConnectionsWithAckMessage(ref MessagePackWriter writer, CloseConnectionsWithAckMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.CloseConnectionsWithAckMessageType);
            writer.Write(message.Reason);
            writer.Write(message.AckId);
            WriteStringArray(ref writer, message.ExcludedList);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteCloseUserConnectionsWithAckMessage(ref MessagePackWriter writer, CloseUserConnectionsWithAckMessage message)
        {
            writer.WriteArrayHeader(6);
            writer.Write(ServiceProtocolConstants.CloseUserConnectionsWithAckMessageType);
            writer.Write(message.UserId);
            writer.Write(message.Reason);
            writer.Write(message.AckId);
            WriteStringArray(ref writer, message.ExcludedList);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteCloseGroupConnectionsWithAckMessage(ref MessagePackWriter writer, CloseGroupConnectionsWithAckMessage message)
        {
            writer.WriteArrayHeader(6);
            writer.Write(ServiceProtocolConstants.CloseGroupConnectionsWithAckMessageType);
            writer.Write(message.GroupName);
            writer.Write(message.Reason);
            writer.Write(message.AckId);
            WriteStringArray(ref writer, message.ExcludedList);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteConnectionDataMessage(ref MessagePackWriter writer, ConnectionDataMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.ConnectionDataMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.Payload);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteConnectionReconnectMessage(ref MessagePackWriter writer, ConnectionReconnectMessage message)
        {
            writer.WriteArrayHeader(3);
            writer.Write(ServiceProtocolConstants.ConnectionReconnectMessageType);
            writer.Write(message.ConnectionId);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteMultiConnectionDataMessage(ref MessagePackWriter writer, MultiConnectionDataMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.MultiConnectionDataMessageType);
            WriteStringArray(ref writer, message.ConnectionList);
            WritePayloads(ref writer, message.Payloads);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteUserDataMessage(ref MessagePackWriter writer, UserDataMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.UserDataMessageType);
            writer.Write(message.UserId);
            WritePayloads(ref writer, message.Payloads);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteMultiUserDataMessage(ref MessagePackWriter writer, MultiUserDataMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.MultiUserDataMessageType);
            WriteStringArray(ref writer, message.UserList);
            WritePayloads(ref writer, message.Payloads);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteBroadcastDataMessage(ref MessagePackWriter writer, BroadcastDataMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.BroadcastDataMessageType);
            WriteStringArray(ref writer, message.ExcludedList);
            WritePayloads(ref writer, message.Payloads);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteJoinGroupMessage(ref MessagePackWriter writer, JoinGroupMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.JoinGroupMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.GroupName);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteLeaveGroupMessage(ref MessagePackWriter writer, LeaveGroupMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.LeaveGroupMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.GroupName);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteUserJoinGroupMessage(ref MessagePackWriter writer, UserJoinGroupMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.UserJoinGroupMessageType);
            writer.Write(message.UserId);
            writer.Write(message.GroupName);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteUserLeaveGroupMessage(ref MessagePackWriter writer, UserLeaveGroupMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.UserLeaveGroupMessageType);
            writer.Write(message.UserId);
            writer.Write(message.GroupName);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteUserJoinGroupWithAckMessage(ref MessagePackWriter writer, UserJoinGroupWithAckMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.UserJoinGroupWithAckMessageType);
            writer.Write(message.UserId);
            writer.Write(message.GroupName);
            writer.Write(message.AckId);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteUserLeaveGroupWithAckMessage(ref MessagePackWriter writer, UserLeaveGroupWithAckMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.UserLeaveGroupWithAckMessageType);
            writer.Write(message.UserId);
            writer.Write(message.GroupName);
            writer.Write(message.AckId);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteGroupBroadcastDataMessage(ref MessagePackWriter writer, GroupBroadcastDataMessage message)
        {
            writer.WriteArrayHeader(7);
            writer.Write(ServiceProtocolConstants.GroupBroadcastDataMessageType);
            writer.Write(message.GroupName);
            WriteStringArray(ref writer, message.ExcludedList);
            WritePayloads(ref writer, message.Payloads);
            message.WriteExtensionMembers(ref writer);
            WriteStringArray(ref writer, message.ExcludedUserList);
            writer.Write(message.CallerUserId);
        }

        private static void WriteMultiGroupBroadcastDataMessage(ref MessagePackWriter writer, MultiGroupBroadcastDataMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.MultiGroupBroadcastDataMessageType);
            WriteStringArray(ref writer, message.GroupList);
            WritePayloads(ref writer, message.Payloads);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteServiceErrorMessage(ref MessagePackWriter writer, ServiceErrorMessage message)
        {
            writer.WriteArrayHeader(2);
            writer.Write(ServiceProtocolConstants.ServiceErrorMessageType);
            writer.Write(message.ErrorMessage);
        }

        private static void WriteServiceEventMessage(ref MessagePackWriter writer, ServiceEventMessage message)
        {
            writer.WriteArrayHeader(6);
            writer.Write(ServiceProtocolConstants.ServiceEventMessageType);
            writer.Write((int)message.Type);
            writer.Write(message.Id);
            writer.Write((int)message.Kind);
            writer.Write(message.Message);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteJoinGroupWithAckMessage(ref MessagePackWriter writer, JoinGroupWithAckMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.JoinGroupWithAckMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.GroupName);
            writer.Write(message.AckId);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteLeaveGroupWithAckMessage(ref MessagePackWriter writer, LeaveGroupWithAckMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.LeaveGroupWithAckMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.GroupName);
            writer.Write(message.AckId);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteCheckUserInGroupWithAckMessage(ref MessagePackWriter writer, CheckUserInGroupWithAckMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.CheckUserInGroupWithAckMessageType);
            writer.Write(message.UserId);
            writer.Write(message.GroupName);
            writer.Write(message.AckId);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteCheckGroupExistenceWithAckMessage(ref MessagePackWriter writer, CheckGroupExistenceWithAckMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.CheckGroupExistenceWithAckMessageType);
            writer.Write(message.GroupName);
            writer.Write(message.AckId);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteCheckConnectionExistenceWithAckMessage(ref MessagePackWriter writer, CheckConnectionExistenceWithAckMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.CheckConnectionExistenceWithAckMessageType);
            writer.Write(message.ConnectionId);
            writer.Write(message.AckId);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteCheckUserExistenceWithAckMessage(ref MessagePackWriter writer, CheckUserExistenceWithAckMessage message)
        {
            writer.WriteArrayHeader(4);
            writer.Write(ServiceProtocolConstants.CheckUserExistenceWithAckMessageType);
            writer.Write(message.UserId);
            writer.Write(message.AckId);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteAckMessage(ref MessagePackWriter writer, AckMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.AckMessageType);
            writer.Write(message.AckId);
            writer.Write(message.Status);
            writer.Write(message.Message);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteClientInvocationMessage(ref MessagePackWriter writer, ClientInvocationMessage message)
        {
            writer.WriteArrayHeader(6);
            writer.Write(ServiceProtocolConstants.ClientInvocationMessageType);
            writer.Write(message.InvocationId);
            writer.Write(message.ConnectionId);
            writer.Write(message.CallerServerId);
            WritePayloads(ref writer, message.Payloads);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteClientCompletionMessage(ref MessagePackWriter writer, ClientCompletionMessage message)
        {
            writer.WriteArrayHeader(7);
            writer.Write(ServiceProtocolConstants.ClientCompletionMessageType);
            writer.Write(message.InvocationId);
            writer.Write(message.ConnectionId);
            writer.Write(message.CallerServerId);
            writer.Write(message.Protocol);
            writer.Write(message.Payload);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteErrorCompletionMessage(ref MessagePackWriter writer, ErrorCompletionMessage message)
        {
            writer.WriteArrayHeader(6);
            writer.Write(ServiceProtocolConstants.ErrorCompletionMessageType);
            writer.Write(message.InvocationId);
            writer.Write(message.ConnectionId);
            writer.Write(message.CallerServerId);
            writer.Write(message.Error);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteServiceMappingMessage(ref MessagePackWriter writer, ServiceMappingMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.ServiceMappingMessageType);
            writer.Write(message.InvocationId);
            writer.Write(message.ConnectionId);
            writer.Write(message.InstanceId);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteConnectionFlowControlMessage(ref MessagePackWriter writer, ConnectionFlowControlMessage message)
        {
            writer.WriteArrayHeader(5);
            writer.Write(ServiceProtocolConstants.ConnectionFlowControlMessageType);
            writer.Write(message.ConnectionId);
            writer.WriteInt32((int)message.ConnectionType);
            writer.WriteInt32((int)message.Operation);
            message.WriteExtensionMembers(ref writer);
        }

        private static void WriteStringArray(ref MessagePackWriter writer, IReadOnlyList<string>? array)
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

        private static AccessKeyRequestMessage CreateAccessKeyRequestMessage(ref MessagePackReader reader, int arrayLength)
        {
            var message = new AccessKeyRequestMessage()
            {
                Token = ReadString(ref reader, "token"),
                Kid = ReadString(ref reader, "kid"),
            };
            message.ReadExtensionMembers(ref reader);
            return message;
        }

        private static AccessKeyResponseMessage CreateAccessKeyResponseMessage(ref MessagePackReader reader, int arrayLength)
        {
            var message = new AccessKeyResponseMessage()
            {
                Kid = ReadString(ref reader, "kid"),
                AccessKey = ReadString(ref reader, "accessKey"),
                ErrorType = ReadString(ref reader, "errorType"),
                ErrorMessage = ReadString(ref reader, "errorMessage"),
            };
            message.ReadExtensionMembers(ref reader);
            return message;
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
            if (arrayLength >= 6)
            {
                result.ReadExtensionMembers(ref reader);
            }
            if (arrayLength >= 7)
            {
                result.AllowStatefulReconnects = ReadBoolean(ref reader, "enableStatefulReconnects");
            }
            return result;
        }

        private static HandshakeResponseMessage CreateHandshakeResponseMessage(ref MessagePackReader reader, int arrayLength)
        {
            var errorMessage = ReadString(ref reader, "errorMessage");
            var result = new HandshakeResponseMessage(errorMessage);
            if (arrayLength >= 3)
            {
                result.ReadExtensionMembers(ref reader);
            }
            if (arrayLength >= 4)
            {
                result.ConnectionId = ReadString(ref reader, "connectionId");
            }
            return result;
        }

        private static PingMessage CreatePingMessage(ref MessagePackReader reader, int arrayLength)
        {
            if (arrayLength > 1)
            {
                var length = arrayLength - 1;
                var values = new string[length];
                for (int i = 0; i < length; i++)
                {
                    values[i] = ReadString(ref reader, "messages[{0}]", i);
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
            if (arrayLength >= 5)
            {
                var headers = ReadHeaders(ref reader);
                var queryString = ReadString(ref reader, "queryString");
                var result = new OpenConnectionMessage(connectionId, claims, headers, queryString);
                if (arrayLength >= 6)
                {
                    result.ReadExtensionMembers(ref reader);
                }
                return result;
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
            var headers = arrayLength >= 4 ? ReadHeaders(ref reader) : new Dictionary<string, StringValues>();
            var result = new CloseConnectionMessage(connectionId, errorMessage, headers);
            if (arrayLength >= 5)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        [Obsolete]
        private static CloseConnectionWithAckMessage CreateCloseConnectionWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var reason = ReadString(ref reader, "reason");
            var ackId = ReadInt32(ref reader, "ackId");
            var result = new CloseConnectionWithAckMessage(connectionId, ackId)
            {
                Reason = reason
            };
            if (arrayLength >= 5)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        [Obsolete]
        private static CloseConnectionsWithAckMessage CreateCloseConnectionsWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var reason = ReadString(ref reader, "reason");
            var ackId = ReadInt32(ref reader, "ackId");
            var excluded = ReadStringArray(ref reader, "excluded");

            var result = new CloseConnectionsWithAckMessage(ackId)
            {
                Reason = reason,
                ExcludedList = excluded
            };
            if (arrayLength >= 5)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static CloseUserConnectionsWithAckMessage CreateCloseUserConnectionsWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var userId = ReadString(ref reader, "userId");
            var reason = ReadString(ref reader, "reason");
            var ackId = ReadInt32(ref reader, "ackId");
            var excluded = ReadStringArray(ref reader, "excluded");

            var result = new CloseUserConnectionsWithAckMessage(userId, ackId)
            {
                Reason = reason,
                ExcludedList = excluded
            };
            if (arrayLength >= 6)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static CloseGroupConnectionsWithAckMessage CreateCloseGroupConnectionsWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var group = ReadString(ref reader, "group");
            var reason = ReadString(ref reader, "reason");
            var ackId = ReadInt32(ref reader, "ackId");
            var excluded = ReadStringArray(ref reader, "excluded");

            var result = new CloseGroupConnectionsWithAckMessage(group, ackId)
            {
                Reason = reason,
                ExcludedList = excluded
            };
            if (arrayLength >= 6)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static ConnectionDataMessage CreateConnectionDataMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var payload = ReadBytes(ref reader, "payload");

            var result = new ConnectionDataMessage(connectionId, payload);
            if (arrayLength >= 4)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static ConnectionReconnectMessage CreateConnectionReconnectMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");

            var result = new ConnectionReconnectMessage(connectionId);
            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static MultiConnectionDataMessage CreateMultiConnectionDataMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionList = ReadStringArray(ref reader, "connectionList");
            var payloads = ReadPayloads(ref reader);

            var result = new MultiConnectionDataMessage(connectionList, payloads);
            if (arrayLength >= 4)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static ServiceMessage CreateUserDataMessage(ref MessagePackReader reader, int arrayLength)
        {
            var userId = ReadString(ref reader, "userId");
            var payloads = ReadPayloads(ref reader);

            var result = new UserDataMessage(userId, payloads);
            if (arrayLength >= 4)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static MultiUserDataMessage CreateMultiUserDataMessage(ref MessagePackReader reader, int arrayLength)
        {
            var userList = ReadStringArray(ref reader, "userList");
            var payloads = ReadPayloads(ref reader);

            var result = new MultiUserDataMessage(userList, payloads);
            if (arrayLength >= 4)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static BroadcastDataMessage CreateBroadcastDataMessage(ref MessagePackReader reader, int arrayLength)
        {
            var excludedList = ReadStringArray(ref reader, "excludedList");
            var payloads = ReadPayloads(ref reader);

            var result = new BroadcastDataMessage(excludedList, payloads);
            if (arrayLength >= 4)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static JoinGroupMessage CreateJoinGroupMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var groupName = ReadString(ref reader, "groupName");

            var result = new JoinGroupMessage(connectionId, groupName);
            if (arrayLength >= 4)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static LeaveGroupMessage CreateLeaveGroupMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var groupName = ReadString(ref reader, "groupName");

            var result = new LeaveGroupMessage(connectionId, groupName);
            if (arrayLength >= 4)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static UserJoinGroupMessage CreateUserJoinGroupMessage(ref MessagePackReader reader, int arrayLength)
        {
            var userId = ReadString(ref reader, "userId");
            var groupName = ReadString(ref reader, "groupName");

            var result = new UserJoinGroupMessage(userId, groupName);
            if (arrayLength >= 4)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static UserLeaveGroupMessage CreateUserLeaveGroupMessage(ref MessagePackReader reader, int arrayLength)
        {
            var userId = ReadString(ref reader, "userId");
            var groupName = ReadString(ref reader, "groupName");

            var result = new UserLeaveGroupMessage(userId, groupName);
            if (arrayLength >= 4)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static UserJoinGroupWithAckMessage CreateUserJoinGroupWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var userId = ReadString(ref reader, "userId");
            var groupName = ReadString(ref reader, "groupName");
            var ackId = ReadInt32(ref reader, "ackId");

            var result = new UserJoinGroupWithAckMessage(userId, groupName, ackId);
            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static UserLeaveGroupWithAckMessage CreateUserLeaveGroupWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var userId = ReadString(ref reader, "userId");
            var groupName = ReadString(ref reader, "groupName");
            var ackId = ReadInt32(ref reader, "ackId");

            var result = new UserLeaveGroupWithAckMessage(userId, groupName, ackId);
            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static GroupBroadcastDataMessage CreateGroupBroadcastDataMessage(ref MessagePackReader reader, int arrayLength)
        {
            var groupName = ReadString(ref reader, "groupName");
            var excludedList = ReadStringArray(ref reader, "excludedList");
            var payloads = ReadPayloads(ref reader);

            var result = new GroupBroadcastDataMessage(groupName, excludedList, payloads);
            if (arrayLength >= 5)
            {
                result.ReadExtensionMembers(ref reader);
            }

            if (arrayLength >= 7)
            {
                result.ExcludedUserList = ReadStringArray(ref reader, "excludedUserList");
                result.CallerUserId = ReadString(ref reader, "callerUserId");
            }

            return result;
        }

        private static MultiGroupBroadcastDataMessage CreateMultiGroupBroadcastDataMessage(ref MessagePackReader reader, int arrayLength)
        {
            var groupList = ReadStringArray(ref reader, "groupList");
            var payloads = ReadPayloads(ref reader);

            var result = new MultiGroupBroadcastDataMessage(groupList, payloads);
            if (arrayLength >= 4)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static ServiceErrorMessage CreateServiceErrorMessage(ref MessagePackReader reader)
        {
            var errorMessage = ReadString(ref reader, "errorMessage");

            return new ServiceErrorMessage(errorMessage);
        }

        private static ServiceEventMessage CreateServiceEventMessage(ref MessagePackReader reader)
        {
            var type = ReadInt32(ref reader, "type");
            var id = ReadString(ref reader, "id");
            var kind = ReadInt32(ref reader, "kind");
            var message = ReadString(ref reader, "message");
            var result = new ServiceEventMessage((ServiceEventObjectType)type, id, (ServiceEventKind)kind, message);
            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static JoinGroupWithAckMessage CreateJoinGroupWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var groupName = ReadString(ref reader, "groupName");
            var ackId = ReadInt32(ref reader, "ackId");

            var result = new JoinGroupWithAckMessage(connectionId, groupName, ackId);
            if (arrayLength >= 5)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static LeaveGroupWithAckMessage CreateLeaveGroupWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var groupName = ReadString(ref reader, "groupName");
            var ackId = ReadInt32(ref reader, "ackId");

            var result = new LeaveGroupWithAckMessage(connectionId, groupName, ackId);
            if (arrayLength >= 5)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static CheckUserInGroupWithAckMessage CreateCheckUserInGroupWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var userId = ReadString(ref reader, "userId");
            var groupName = ReadString(ref reader, "groupName");
            var ackId = ReadInt32(ref reader, "ackId");

            var result = new CheckUserInGroupWithAckMessage(userId, groupName, ackId);
            if (arrayLength >= 5)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static CheckGroupExistenceWithAckMessage CreateGroupExistenceWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var groupName = ReadString(ref reader, "groupName");
            var ackId = ReadInt32(ref reader, "ackId");

            var result = new CheckGroupExistenceWithAckMessage(groupName, ackId);
            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static CheckConnectionExistenceWithAckMessage CreateCheckConnectionExistenceWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var ackId = ReadInt32(ref reader, "ackId");

            var result = new CheckConnectionExistenceWithAckMessage(connectionId, ackId);
            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static CheckUserExistenceWithAckMessage CreateCheckUserExistenceWithAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var userId = ReadString(ref reader, "userId");
            var ackId = ReadInt32(ref reader, "ackId");

            var result = new CheckUserExistenceWithAckMessage(userId, ackId);
            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static AckMessage CreateAckMessage(ref MessagePackReader reader, int arrayLength)
        {
            var ackId = ReadInt32(ref reader, "ackId");
            var status = ReadInt32(ref reader, "status");
            var message = ReadString(ref reader, "message");

            var result = new AckMessage(ackId, status, message);
            if (arrayLength >= 5)
            {
                result.ReadExtensionMembers(ref reader);
            }
            return result;
        }

        private static ClientInvocationMessage CreateClientInvocationMessage(ref MessagePackReader reader, int arrayLength)
        {
            var invocationId = ReadString(ref reader, "invocationId");
            var connectionId = ReadString(ref reader, "connectionId");
            var callerServerId = ReadString(ref reader, "callerServerId");
            var payloads = ReadPayloads(ref reader);

            var result = new ClientInvocationMessage(invocationId, connectionId, callerServerId, payloads);
            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static ClientCompletionMessage CreateClientCompletionMessage(ref MessagePackReader reader, int arrayLength)
        {
            var invocationId = ReadString(ref reader, "invocationId");
            var connectionId = ReadString(ref reader, "connectionId");
            var callerServerId = ReadString(ref reader, "callerServerId");
            var protocol = ReadString(ref reader, "protocol");
            var payload = ReadBytes(ref reader, "payload");

            var result = new ClientCompletionMessage(invocationId, connectionId, callerServerId, protocol, payload);

            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static ErrorCompletionMessage CreateErrorCompletionMessage(ref MessagePackReader reader, int arrayLength)
        {
            var invocationId = ReadString(ref reader, "invocationId");
            var connectionId = ReadString(ref reader, "connectionId");
            var callerServerId = ReadString(ref reader, "callerServerId");
            var error = ReadString(ref reader, "error");

            var result = new ErrorCompletionMessage(invocationId, connectionId, callerServerId, error);

            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static ServiceMappingMessage CreateServiceMappingMessage(ref MessagePackReader reader, int arrayLength)
        {
            var invocationId = ReadString(ref reader, "invocationId");
            var connectionId = ReadString(ref reader, "connectionId");
            var instanceId = ReadString(ref reader, "instanceId");

            var result = new ServiceMappingMessage(invocationId, connectionId, instanceId);

            result.ReadExtensionMembers(ref reader);
            return result;
        }

        private static ConnectionFlowControlMessage CreateConnectionFlowControlMessage(ref MessagePackReader reader, int arrayLength)
        {
            var connectionId = ReadString(ref reader, "connectionId");
            var connectionType = ReadInt32(ref reader, "connectionType");
            var operation = ReadInt32(ref reader, "operation");

            switch (connectionType)
            {
                case (int)ConnectionType.Client:
                case (int)ConnectionType.Server:
                    break;
                default:
                    throw new InvalidDataException($"Unsupported connection type: {connectionType}");
            }

            switch (operation)
            {
                case (int)ConnectionFlowControlOperation.Pause:
                case (int)ConnectionFlowControlOperation.PauseAck:
                case (int)ConnectionFlowControlOperation.Resume:
                case (int)ConnectionFlowControlOperation.Offline:
                    break;
                default:
                    throw new InvalidDataException($"Unsupported operation: {operation}");
            }

            var result = new ConnectionFlowControlMessage(
                connectionId,
                (ConnectionFlowControlOperation)operation,
                (ConnectionType)connectionType);
            return result;
        }

        private static Claim[] ReadClaims(ref MessagePackReader reader)
        {
            var claimCount = ReadMapLength(ref reader, "claims");
            if (claimCount > 0)
            {
                var claims = new Claim[claimCount];

                for (var i = 0; i < claimCount; i++)
                {
                    var type = ReadString(ref reader, "claims[{0}].Type", i);
                    var value = ReadString(ref reader, "claims[{0}].Value", i);
                    claims[i] = new Claim(type, value);
                }

                return claims;
            }

            return [];
        }

        private static IDictionary<string, ReadOnlyMemory<byte>> ReadPayloads(ref MessagePackReader reader)
        {
            var payloadCount = ReadMapLength(ref reader, "payloads");
            if (payloadCount > 0)
            {
                var payloads = new ArrayDictionary<string, ReadOnlyMemory<byte>>((int)payloadCount, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < payloadCount; i++)
                {
                    var key = ReadString(ref reader, "payloads[{0}].key", i);
                    var value = ReadBytes(ref reader, "payloads[{0}].value", i);
                    payloads.Add(key, value);
                }

                return payloads;
            }

            return EmptyReadOnlyMemoryDictionary;
        }

        private static IDictionary<string, StringValues> ReadHeaders(ref MessagePackReader reader)
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

            return EmptyStringValuesDictionaryIgnoreCase;
        }

        private static bool ReadBoolean(ref MessagePackReader reader, string field)
        {
            try
            {
                return reader.ReadBoolean();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Reading '{field}' as Boolean failed.", ex);
            }
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

        private static string ReadString(ref MessagePackReader reader, string formatField, int param)
        {
            try
            {
                return reader.ReadString();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Reading '{string.Format(formatField, param)}' as String failed.", ex);
            }
        }

        private static string ReadString(ref MessagePackReader reader, string formatField, string param1, int param2)
        {
            try
            {
                return reader.ReadString();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Reading '{string.Format(formatField, param1, param2)}' as String failed.", ex);
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
                    array[i] = ReadString(ref reader, "{0}[{1}]", field, i);
                }

                return array;
            }

            return [];
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

        private static byte[] ReadBytes(ref MessagePackReader reader, string formatField, int param)
        {
            try
            {
                return reader.ReadBytes()?.ToArray() ?? Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Reading '{string.Format(formatField, param)}' as Byte[] failed.", ex);
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