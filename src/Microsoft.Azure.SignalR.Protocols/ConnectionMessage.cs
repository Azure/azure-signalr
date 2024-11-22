// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Security.Claims;

using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// Base class of connection-specific messages between Azure SignalR Service and SDK.
    /// </summary>
    public abstract class ConnectionMessage : ExtensibleServiceMessage
    {
        protected ConnectionMessage(string connectionId)
        {
            ConnectionId = connectionId;
        }

        /// <summary>
        /// Gets or sets the connection Id.
        /// </summary>
        public string ConnectionId { get; set; }
    }

    /// <summary>
    /// A open-connection message.
    /// </summary>
    public class OpenConnectionMessage : ConnectionMessage, IHasProtocol, IMessageWithTracingId
    {
        /// <summary>
        /// Gets or sets the tracing Id
        /// </summary>
        public ulong? TracingId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenConnectionMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="claims">An array of <see cref="Claim"/> associated with the connection.</param>
        public OpenConnectionMessage(string connectionId, Claim[]? claims)
            : this(connectionId, claims, new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase), string.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenConnectionMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="claims">An array of <see cref="Claim"/> associated with the connection.</param>
        /// <param name="headers">A <see cref="IDictionary{TKey,TValue}"/> associated with the connection.</param>
        /// <param name="queryString">Query string associated with the connection.</param>
        public OpenConnectionMessage(string connectionId, Claim[]? claims, IDictionary<string, StringValues> headers, string queryString)
            : base(connectionId)
        {
            Claims = claims ?? [];
            Headers = headers;
            QueryString = queryString;
        }

        /// <summary>
        /// Gets or sets the associated claims.
        /// </summary>
        public Claim[] Claims { get; set; } = [];

        /// <summary>
        /// Gets or sets the associated headers.
        /// </summary>
        public IDictionary<string, StringValues> Headers { get; set; }

        /// <summary>
        /// Gets or sets the associated query string.
        /// </summary>
        public string QueryString { get; set; }

        /// <summary>
        /// Gets or sets the protocol for new connection.
        /// </summary>
        public string? Protocol { get; set; }
    }

    /// <summary>
    /// A close-connection message.
    /// </summary>
    public class CloseConnectionMessage : ConnectionMessage, IMessageWithTracingId
    {
        /// <summary>
        /// Gets or sets the tracing Id
        /// </summary>
        public ulong? TracingId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloseConnectionMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="errorMessage">Optional error message.</param>
        /// <param name="headers">A <see cref="IDictionary{TKey,TValue}"/> associated with the connection.</param>
        public CloseConnectionMessage(string connectionId, string errorMessage, IDictionary<string, StringValues>? headers = null) : base(connectionId)
        {
            ErrorMessage = errorMessage ?? "";
            Headers = headers ?? new Dictionary<string, StringValues>();
        }

        /// <summary>
        /// Test only
        /// </summary>
        /// <param name="connectionId"></param>
        public CloseConnectionMessage(string connectionId) : this(connectionId, "")
        {
        }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the associated headers.
        /// </summary>
        public IDictionary<string, StringValues> Headers { get; set; }
    }

    /// <summary>
    /// A connection data message.
    /// </summary>
    public class ConnectionDataMessage : ConnectionMessage, IMessageWithTracingId, IHasDataMessageType, IPartializable, IPartitionableMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionDataMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="payload">Binary data to be delivered.</param>
        /// <param name="tracingId">The tracing Id of the message</param>
        public ConnectionDataMessage(string connectionId, ReadOnlyMemory<byte> payload, ulong? tracingId = null)
            : this(connectionId, new ReadOnlySequence<byte>(payload), tracingId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionDataMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="payload">Binary data to be delivered.</param>
        /// <param name="tracingId">The tracing Id of the message</param>
        public ConnectionDataMessage(string connectionId, ReadOnlySequence<byte> payload, ulong? tracingId = null) : base(connectionId)
        {
            Payload = payload;
            TracingId = tracingId;
        }

        /// <summary>
        /// Gets or sets the binary payload.
        /// </summary>
        public ReadOnlySequence<byte> Payload { get; set; }

        /// <summary>
        /// Gets or sets the message ID
        /// </summary>
        public ulong? TracingId { get; set; }

        /// <summary>
        /// Gets or sets the type of payload.
        /// </summary>
        public DataMessageType Type { get; set; }

        /// <summary>
        /// Gets or sets the payload is partial or not.
        /// </summary>
        public bool IsPartial { get; set; }

        public byte PartitionKey => GeneratePartitionKey(ConnectionId);
    }

    /// <summary>
    /// A signal for client connection reconnect.
    /// </summary>
    public class ConnectionReconnectMessage : ConnectionMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionReconnectMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        public ConnectionReconnectMessage(string connectionId) : base(connectionId) { }
    }

    /// <summary>
    /// Base class of client invocation completion messages between Azure SignalR Service and SDK.
    /// </summary>
    public abstract class ServiceCompletionMessage : ConnectionMessage, IMessageWithTracingId
    {
        public ServiceCompletionMessage(string invocationId, string connectionId, string callerServerId, ulong? tracingId = null)
            : base(connectionId)
        {
            InvocationId = invocationId;
            CallerServerId = callerServerId;
            TracingId = tracingId;
        }

        /// <summary>
        /// Gets or sets the client invocation Id of pending connection.
        /// </summary>
        public string InvocationId { get; set; }

        /// <summary>
        /// Gets or sets the server Id which parse the completion message.
        /// </summary>
        public string CallerServerId { get; set; }

        /// <summary>
        /// Gets or sets the message ID
        /// </summary>
        public ulong? TracingId { get; set; }
    }

    /// <summary>
    /// A data message of client invocation completion result.
    /// </summary>
    public class ClientCompletionMessage : ServiceCompletionMessage, IHasProtocol
    {
        /// <summary>
        /// Initialize a new instance of <see cref="ClientCompletionMessage"/> class.
        /// </summary>
        /// <param name="invocationId">The Id of the invocation that has completed.</param>
        /// <param name="connectionId">The client connection Id that complete the invocation.</param>
        /// <param name="callerServerId">The serverId that wrap the completion result.</param>
        /// <param name="protocol">The protocol of the connection.</param>
        /// <param name="payload">The payload of the completion result.</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public ClientCompletionMessage(string invocationId, string connectionId, string callerServerId, string protocol, ReadOnlyMemory<byte> payload, ulong? tracingId = null)
            : base(invocationId, connectionId, callerServerId, tracingId)
        {
            Protocol = protocol;
            Payload = new ReadOnlySequence<byte>(payload);
        }

        /// <summary>
        /// Gets or sets the connection protocol.
        /// </summary>
        public string? Protocol { get; set; }

        /// <summary>
        /// Gets or sets the binary payload.
        /// </summary>
        public ReadOnlySequence<byte> Payload { get; set; }
    }

    /// <summary>
    /// An error response indicates client invocation failed with details.
    /// </summary>
    public class ErrorCompletionMessage : ServiceCompletionMessage
    {
        /// <summary>
        /// Initialize a new instance of <see cref="ClientCompletionMessage"/> class with error information.
        /// </summary>
        /// <param name="invocationId">The Id of the invocation that has completed.</param>
        /// <param name="connectionId">The client connection Id that complete the invocation.</param>
        /// <param name="callerServerId">The serverId that wrap the completion result.</param>
        /// <param name="error">The error information about invacation failure.</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public ErrorCompletionMessage(string invocationId, string connectionId, string callerServerId, string error, ulong? tracingId = null)
            : base(invocationId, connectionId, callerServerId, tracingId)
        {
            Error = error;
        }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string Error { get; set; }
    }
}
