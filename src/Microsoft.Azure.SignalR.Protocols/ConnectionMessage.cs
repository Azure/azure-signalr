// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// Base class of connection-specific messages between Azure SignalR Service and SDK.
    /// </summary>
    public abstract class ConnectionMessage : ServiceMessage
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
    public class OpenConnectionMessage : ConnectionMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenConnectionMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="claims">An array of <see cref="Claim"/> associated with the connection.</param>
        public OpenConnectionMessage(string connectionId, Claim[] claims) : base(connectionId)
        {
            Claims = claims;
        }

        /// <summary>
        /// Gets or sets the associated claims.
        /// </summary>
        public Claim[] Claims { get; set; }
    }

    /// <summary>
    /// A close-connection message.
    /// </summary>
    public class CloseConnectionMessage : ConnectionMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CloseConnectionMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        public CloseConnectionMessage(string connectionId) : this(connectionId, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloseConnectionMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="errorMessage">Optional error message.</param>
        public CloseConnectionMessage(string connectionId, string errorMessage) : base(connectionId)
        {
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// A connection data message.
    /// </summary>
    public class ConnectionDataMessage : ConnectionMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionDataMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="payload">Binary data to be delivered.</param>
        public ConnectionDataMessage(string connectionId, ReadOnlyMemory<byte> payload) : base(connectionId)
        {
            Payload = new ReadOnlySequence<byte>(payload);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionDataMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="payload">Binary data to be delivered.</param>
        public ConnectionDataMessage(string connectionId, ReadOnlySequence<byte> payload) : base(connectionId)
        {
            Payload = payload;
        }

        /// <summary>
        /// Gets or sets the binary payload.
        /// </summary>
        public ReadOnlySequence<byte> Payload { get; set; }
    }
}
