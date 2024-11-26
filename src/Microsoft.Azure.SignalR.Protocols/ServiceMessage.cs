// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System;

using MessagePack;

namespace Microsoft.Azure.SignalR.Protocol
{
    public enum ConnectionFlowControlOperation
    {
        /// <summary>
        /// Send from ASRS to the app server.
        /// Asking the client connection to pause sending messages towards ASRS.
        /// </summary>
        Pause = 1,

        /// <summary>
        /// Reply from the app server to ASRS.
        /// Ackknowledge that the message sending towards ASRS has been pasued.
        /// </summary>
        PauseAck = 2,

        /// <summary>
        /// Send from ASRS to the app server.
        /// Asking the client connection to resume sending messages towards ASRS.
        /// </summary>
        Resume = 3,

        /// <summary>
        /// Send from ASRS to the app server.
        /// Asking the app server to send messages through other server connections.
        /// </summary>
        Offline = 4,
    }

    public enum ConnectionType
    {
        /// <summary>
        /// Client connection
        /// </summary>
        Client = 1,
        /// <summary>
        /// Server connection
        /// </summary>
        Server = 2,
    }

    /// <summary>
    /// Interface of ack-able message
    /// </summary>
    public interface IAckableMessage
    {
        int AckId { get; set; }
    }

    public interface IPartitionableMessage
    {
        byte PartitionKey { get; }
    }

    /// <summary>
    /// Base class of messages between Azure SignalR Service and SDK.
    /// </summary>
    public abstract class ServiceMessage
    {
        /// <summary>
        /// Clone should make a copy of everything that may get modified throughout the lifetime of the message
        /// The default implementation is a shallow copy as it fits the current needs.
        /// </summary>
        public virtual ServiceMessage Clone() => (MemberwiseClone() as ServiceMessage)!;

        public static byte GeneratePartitionKey(string input)
        {
            return (byte)((input?.GetHashCode() ?? 0) & 0xFF);
        }
    }

    /// <summary>
    /// Base class of messages between Azure SignalR Service and SDK.
    /// </summary>
    public abstract class ExtensibleServiceMessage : ServiceMessage
    {
        private const int TracingId = 1;

        private const int Ttl = 2;

        private const int Protocol = 3;

        private const int Filter = 4;
        private const int DataMessageType = 5;
        private const int IsPartial = 6;

        internal void WriteExtensionMembers(ref MessagePackWriter writer)
        {
            int count = 0;
            var tracingId = (this as IMessageWithTracingId)?.TracingId;
            if (tracingId != null)
            {
                count++;
            }
            var ttl = (this as IHasTtl)?.Ttl;
            if (ttl != null)
            {
                count++;
            }
            var protocol = (this as IHasProtocol)?.Protocol;
            if (protocol != null)
            {
                count++;
            }

            var filter = (this as IHasSubscriberFilter)?.Filter;
            if (filter != null)
            {
                count++;
            }

            var dataMessageType = (this as IHasDataMessageType)?.Type ?? default;
            if (dataMessageType != default)
            {
                count++;
            }

            var isPartial = (this as IPartializable)?.IsPartial ?? false;
            if (isPartial)
            {
                count++;
            }

            // todo : count more optional fields.
            writer.WriteMapHeader(count);
            if (tracingId != null)
            {
                writer.Write(TracingId);
                writer.Write(tracingId.Value);
            }
            if (ttl != null)
            {
                writer.Write(Ttl);
                writer.Write(ttl.Value);
            }
            if (protocol != null)
            {
                writer.Write(Protocol);
                writer.Write(protocol);
            }
            if (filter != null)
            {
                writer.Write(Filter);
                writer.Write(filter);
            }
            if (dataMessageType != default)
            {
                writer.Write(DataMessageType);
                writer.Write((int)dataMessageType);
            }
            if (isPartial)
            {
                writer.Write(IsPartial);
                writer.Write(isPartial);
            }
            // todo : write more optional fields.
        }

        internal void ReadExtensionMembers(ref MessagePackReader reader)
        {
            int count = reader.ReadMapHeader();
            for (int i = 0; i < count; i++)
            {
                switch (reader.ReadInt32())
                {
                    case TracingId:
                        if (this is IMessageWithTracingId withTracingId)
                        {
                            withTracingId.TracingId = reader.ReadUInt64();
                        }
                        break;
                    case Ttl:
                        if (this is IHasTtl hasTtl)
                        {
                            hasTtl.Ttl = reader.ReadInt32();
                        }
                        break;
                    case Protocol:
                        if (this is IHasProtocol hasProtocol)
                        {
                            hasProtocol.Protocol = reader.ReadString();
                        }
                        break;
                    case Filter:
                        if (this is IHasSubscriberFilter hasFilter)
                        {
                            hasFilter.Filter = reader.ReadString();
                        }
                        break;
                    case DataMessageType:
                        if (this is IHasDataMessageType hasDataMessageType)
                        {
                            hasDataMessageType.Type = (DataMessageType)reader.ReadInt32();
                        }
                        break;
                    case IsPartial:
                        if (this is IPartializable canPartial)
                        {
                            canPartial.IsPartial = reader.ReadBoolean();
                        }
                        break;
                    // todo : more optional fields
                    default:
                        // bypass unknown member.
                        reader.Skip();
                        break;
                }
            }
        }
    }

    /// <summary>
    /// The ASRS send it to the source server connection to initiate a client connection migration.
    /// Indicates that incoming messages are blocked.
    /// </summary>
    /// <param name="connectionId">The client connection ID</param>
    /// <param name="op">The operation</param>
    /// <param name="type">The connection type</param>
    public class ConnectionFlowControlMessage(string connectionId, ConnectionFlowControlOperation op, ConnectionType type = ConnectionType.Client) : ConnectionMessage(connectionId)
    {
        public ConnectionFlowControlOperation Operation { get; } = op;

        public ConnectionType ConnectionType { get; } = type;
    }

    /// <summary>
    /// An access key request message.
    /// </summary>
    public class AccessKeyRequestMessage : ExtensibleServiceMessage
    {
        /// <summary>
        /// Gets or sets the Azure Active Directory token.
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Gets or sets the key Id.
        /// <c>null</c>
        /// </summary>
        public string? Kid { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKeyRequestMessage"/> class.
        /// </summary>
        public AccessKeyRequestMessage()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKeyRequestMessage"/> class.
        /// </summary>
        /// <param name="token"></param>
        public AccessKeyRequestMessage(string token)
        {
            Token = token;
        }
    }

    /// <summary>
    /// An access key response message.
    /// </summary>
    public class AccessKeyResponseMessage : ExtensibleServiceMessage
    {
        /// <summary>
        /// Gets or sets the key Id.
        /// </summary>
        public string? Kid { get; set; }

        /// <summary>
        /// Gets or sets the access key.
        /// </summary>
        public string? AccessKey { get; set; }

        /// <summary>
        /// Gets or sets error type.
        /// </summary>
        public string? ErrorType { get; set; }

        /// <summary>
        /// Gets or sets error message.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKeyResponseMessage"/> class.
        /// </summary>
        public AccessKeyResponseMessage()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKeyResponseMessage"/> class.
        /// </summary>
        /// <param name="kid"></param>
        /// <param name="key"></param>
        public AccessKeyResponseMessage(string kid, string key)
        {
            Kid = kid;
            AccessKey = key;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AccessKeyResponseMessage"/> class.
        /// </summary>
        /// <param name="e"></param>
        public AccessKeyResponseMessage(Exception e)
        {
            ErrorType = e.GetType().Name;
            ErrorMessage = e.Message;
        }
    }

    /// <summary>
    /// A handshake request message.
    /// </summary>
    public class HandshakeRequestMessage : ExtensibleServiceMessage
    {
        /// <summary>
        /// Gets or sets the requested protocol version.
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the type of service connection.
        /// </summary>
        /// <value>
        /// <list type="bullet">
        /// <item>0, Default, it can carry clients, service runtime should always accept this kind of connection.</item>
        /// <item>1, OnDemand, creating when service requested more connections, it can carry clients, but it may be rejected by service runtime.</item>
        /// <item>2, Weak, it can not carry clients, but it can send message.</item>
        /// </list>
        /// </value>
        public int ConnectionType { get; set; }

        /// <summary>
        /// Gets or sets the target of service connection, only work for OnDemand connections.
        /// </summary>
        public string? Target { get; set; }

        /// <summary>
        /// Gets or sets the migratable flag.
        /// <value>
        /// <list type="bullet">
        /// <item>0, Off, a client connection can not be migrated to another server.</item>
        /// <item>1, ShutdownOnly, a client connection can be migrated only if the pairing server was shutdown gracefully.</item>
        /// <item>2, Any, a client connection can be migrated even if the pairing server connection was dropped accidentally. (may cause data loss)</item>
        /// </list>
        /// </value>
        /// </summary>
        public int MigrationLevel { get; set; }

        /// <summary>
        /// Gets or sets the allowing of client stateful reconnects.
        /// </summary>
        public bool AllowStatefulReconnects { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HandshakeRequestMessage"/> class.
        /// </summary>
        /// <param name="version">version</param>
        public HandshakeRequestMessage(int version)
        {
            Version = version;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HandshakeRequestMessage"/> class.
        /// </summary>
        /// <param name="version">version</param>
        /// <param name="connectionType">connection type</param>
        /// <param name="migrationLevel">migration level</param>
        public HandshakeRequestMessage(int version, int connectionType, int migrationLevel)
        {
            Version = version;
            ConnectionType = connectionType;
            MigrationLevel = migrationLevel;
        }
    }

    /// <summary>
    /// A handshake response message.
    /// </summary>
    public class HandshakeResponseMessage : ExtensibleServiceMessage
    {
        /// <summary>
        /// Gets or sets the optional error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the id of this connection.
        /// </summary>
        public string? ConnectionId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HandshakeResponseMessage"/> class.
        /// </summary>
        public HandshakeResponseMessage() : this(string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HandshakeResponseMessage"/> class.
        /// </summary>
        /// <param name="errorMessage">An optional response error message. A <c>null</c> or empty error message indicates a successful handshake.</param>
        public HandshakeResponseMessage(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// A ping message.
    /// </summary>
    public class PingMessage : ServiceMessage
    {
        /// <summary>
        /// A static ping message.
        /// </summary>
        public static PingMessage Instance = new PingMessage();

        public string[] Messages { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// A service error message
    /// </summary>
    public class ServiceErrorMessage : ServiceMessage
    {
        /// <summary>
        /// Gets or sets the error message
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceErrorMessage"/> class.
        /// </summary>
        /// <param name="errorMessage">An error message.</param>
        public ServiceErrorMessage(string errorMessage)
        {
            ErrorMessage = errorMessage;
        }
    }

    /// <summary>
    /// A service warning message
    /// </summary>
    public class ServiceEventMessage : ExtensibleServiceMessage
    {
        /// <summary>
        /// Gets or sets the type of event object.
        /// </summary>
        public ServiceEventObjectType Type { get; set; }

        /// <summary>
        /// Gets or sets the id of event object.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the kind of event.
        /// </summary>
        public ServiceEventKind Kind { get; set; }

        /// <summary>
        /// Gets or sets the message of event.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceEventMessage"/> class.
        /// </summary>
        /// <param name="type">A type of event object.</param>
        /// <param name="id">An id of event object.</param>
        /// <param name="kind">A kind of event.</param>
        /// <param name="message">A message of event.</param>
        public ServiceEventMessage(ServiceEventObjectType type, string id, ServiceEventKind kind, string message)
        {
            Type = type;
            Id = id;
            Kind = kind;
            Message = message;
        }
    }

    /// <summary>
    /// An ack message to response ack-able message
    /// </summary>
    public class AckMessage : ExtensibleServiceMessage
    {
        /// <summary>
        /// Gets or sets the ack id.
        /// </summary>
        public int AckId { get; set; }

        /// <summary>
        /// Gets or sets the status code
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Gets or sets the ack message
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AckMessage"/> class.
        /// </summary>
        /// <param name="ackId">The ack Id</param>
        /// <param name="status">The status code</param>
        public AckMessage(int ackId, int status) : this(ackId, status, string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AckMessage"/> class.
        /// </summary>
        /// <param name="ackId">The ack Id</param>
        /// <param name="status">The status code</param>
        /// <param name="message">The ack message</param>
        public AckMessage(int ackId, int status, string message)
        {
            AckId = ackId;
            Status = status;
            Message = message;
        }
    }

    /// <summary>
    /// A message indicates the mapping of client invocation with service instance.
    /// </summary>
    public class ServiceMappingMessage : ExtensibleServiceMessage
    {
        /// <summary>
        /// Gets or sets the invocation Id.
        /// </summary>
        public string InvocationId { get; set; }

        /// <summary>
        /// Gets or sets the connection Id.
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// Gets or set the service instance Id.
        /// </summary>
        public string InstanceId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceMappingMessage"/> class.
        /// </summary>
        /// <param name="invocationId">The invocation Id.</param>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="instanceId">The service instance Id.</param>
        public ServiceMappingMessage(string invocationId, string connectionId, string instanceId)
        {
            InvocationId = invocationId;
            ConnectionId = connectionId;
            InstanceId = instanceId;
        }
    }
}
