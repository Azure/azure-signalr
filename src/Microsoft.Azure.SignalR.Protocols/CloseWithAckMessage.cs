// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Protocol
{
    public abstract class CloseWithAckMessage : ExtensibleServiceMessage, IAckableMessage, IMessageWithTracingId
    {
        /// <summary>
        /// Gets or sets the ack Id.
        /// </summary>
        public int AckId { get; set; }

        /// <summary>
        /// Gets or sets the tracing Id.
        /// </summary>
        public ulong? TracingId { get; set; }

        /// <summary>
        /// Gets or sets the reason for the close.
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Creates a copy of the message
        /// </summary>
        public abstract ServiceMessage Clone { get; }

        public CloseWithAckMessage(int ackId)
        {
            AckId = ackId;
        }
    }

    public abstract class CloseMultiConnectionsWithAckMessage : CloseWithAckMessage
    {
        /// <summary>
        /// Gets or sets the list of excluded connection Ids.
        /// </summary>
        public IReadOnlyList<string> ExcludedList { get; set; }

        public CloseMultiConnectionsWithAckMessage(int ackId) : base(ackId) { }
    }


    /// <summary>
    /// A close-connection message.
    /// </summary>
    public class CloseConnectionWithAckMessage : CloseWithAckMessage
    {
        /// <summary>
        /// Gets or sets the connection Id for the connection.
        /// </summary>
        public string ConnectionId { get; }

        public override ServiceMessage Clone => new CloseConnectionWithAckMessage(ConnectionId, AckId) 
        { 
            TracingId = TracingId,
            Reason = Reason
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="CloseConnectionWithAckMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="ackId">The ack Id for the message.</param>
        public CloseConnectionWithAckMessage(string connectionId, int ackId) : base(ackId)
        {
            ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
        }
    }

    /// <summary>
    /// Close all the connections in the hub.
    /// </summary>
    public class CloseConnectionsWithAckMessage : CloseMultiConnectionsWithAckMessage
    {
        public override ServiceMessage Clone => new CloseConnectionsWithAckMessage(AckId)
        {
            TracingId = TracingId,
            Reason = Reason,
            ExcludedList = ExcludedList
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="CloseConnectionsWithAckMessage"/> class.
        /// </summary>
        /// <param name="ackId">The ack Id for the message.</param>
        public CloseConnectionsWithAckMessage(int ackId) : base(ackId) { }
    }

    /// <summary>
    /// Close connections for a user.
    /// </summary>
    public class CloseUserConnectionsWithAckMessage : CloseMultiConnectionsWithAckMessage
    {
        /// <summary>
        /// Gets or sets the user Id.
        /// </summary>
        public string UserId { get; set; }

        public override ServiceMessage Clone => new CloseUserConnectionsWithAckMessage(UserId, AckId)
        {
            TracingId = TracingId,
            Reason = Reason,
            ExcludedList = ExcludedList
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="CloseUserConnectionsWithAckMessage"/> class.
        /// </summary>
        /// <param name="userId">The user Id for the message.</param>
        /// <param name="ackId">The ack Id for the message.</param>
        public CloseUserConnectionsWithAckMessage(string userId, int ackId) : base(ackId)
        {
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
        }
    }

    /// <summary>
    /// Close connections in a group.
    /// </summary>
    public class CloseGroupConnectionsWithAckMessage : CloseMultiConnectionsWithAckMessage
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; }

        public override ServiceMessage Clone => new CloseGroupConnectionsWithAckMessage(GroupName, AckId)
        {
            TracingId = TracingId,
            Reason = Reason,
            ExcludedList = ExcludedList
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="CloseGroupConnectionsWithAckMessage"/> class.
        /// </summary>
        /// <param name="groupName">The group name for the message.</param>
        /// <param name="ackId">The ack Id for the message.</param>
        public CloseGroupConnectionsWithAckMessage(string groupName, int ackId) : base(ackId)
        {
            GroupName = groupName ?? throw new ArgumentNullException(nameof(groupName));
        }
    }
}
