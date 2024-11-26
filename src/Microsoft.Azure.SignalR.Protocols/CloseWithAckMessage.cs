// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;

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
        public string? Reason { get; set; }

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
        public IReadOnlyList<string> ExcludedList { get; set; } = [];

        public CloseMultiConnectionsWithAckMessage(int ackId) : base(ackId) { }
    }


    /// <summary>
    /// A close-connection message.
    /// </summary>
    [Obsolete("Please use CloseConnectionMessage")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    public class CloseConnectionWithAckMessage : CloseWithAckMessage
    {
        /// <summary>
        /// Gets or sets the connection Id for the connection.
        /// </summary>
        public string ConnectionId { get; }

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
    [Obsolete("Please use CloseConnectionMessage")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    public class CloseConnectionsWithAckMessage : CloseMultiConnectionsWithAckMessage
    {
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
    public class CloseGroupConnectionsWithAckMessage : CloseMultiConnectionsWithAckMessage, IPartitionableMessage
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; }

        public byte PartitionKey => GeneratePartitionKey(GroupName);

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
