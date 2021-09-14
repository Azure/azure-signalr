// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Protocol
{
    public abstract class CloseWithAckMessage : ExtensibleServiceMessage, IAckableMessage, IMessageWithTracingId
    {
        /// <summary>
        /// Gets or sets the ack id.
        /// </summary>
        public int AckId { get; set; }

        /// <summary>
        /// Gets or sets the tracing Id
        /// </summary>
        public ulong? TracingId { get; set; }

        /// <summary>
        /// Gets or sets the reason closing the connection
        /// </summary>
        public string Reason { get; set; }

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
        /// Gets or sets the connection Id for the connection
        /// </summary>
        public string ConnectionId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CloseConnectionMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="ackId">The ack Id for the message.</param>
        public CloseConnectionWithAckMessage(string connectionId, int ackId) : base(ackId)
        {
            ConnectionId = connectionId;
        }
    }

    /// <summary>
    /// Close connections in a hub.
    /// </summary>
    public class CloseConnectionsWithAckMessage : CloseMultiConnectionsWithAckMessage
    {
        public CloseConnectionsWithAckMessage(int ackId) : base(ackId) { }
    }

    /// <summary>
    /// Close connections for a user.
    /// </summary>
    public class CloseUserConnectionsWithAckMessage : CloseMultiConnectionsWithAckMessage
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public string UserId { get; set; }

        public CloseUserConnectionsWithAckMessage(string userId, int ackId) : base(ackId)
        {
            UserId = userId;
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

        public CloseGroupConnectionsWithAckMessage(string groupName, int ackId) : base(ackId)
        {
            GroupName = groupName;
        }
    }
}
