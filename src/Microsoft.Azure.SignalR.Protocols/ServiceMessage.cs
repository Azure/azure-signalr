// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// Base class of messages between Azure SignalR Service and SDK.
    /// </summary>
    public abstract class ServiceMessage
    {
    }

    /// <summary>
    /// Interface of ack-able message 
    /// </summary>
    public interface IAckableMessage
    {
        int AckId { get; set; }
    }

    /// <summary>
    /// A handshake request message.
    /// </summary>
    public class HandshakeRequestMessage : ServiceMessage
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
        /// Gets or sets the target of service connection, only work for OnDemand connections.
        /// </summary>
        public string Target { get; set; }

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
    public class HandshakeResponseMessage : ServiceMessage
    {
        /// <summary>
        /// Gets or sets the optional error message.
        /// </summary>
        public string ErrorMessage { get; set; }

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
    /// A ack message to response ack-able message
    /// </summary>
    public class AckMessage : ServiceMessage
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
}
