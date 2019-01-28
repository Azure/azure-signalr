// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// A join-group message.
    /// </summary>
    public class JoinGroupMessage : ServiceMessage
    {
        /// <summary>
        /// Gets or sets the connection Id.
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JoinGroupMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="groupName">The group name, to which the connection will join.</param>
        public JoinGroupMessage(string connectionId, string groupName)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
        }
    }

    /// <summary>
    /// A leave-group message.
    /// </summary>
    public class LeaveGroupMessage : ServiceMessage
    {
        /// <summary>
        /// Gets or sets the connection Id.
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaveGroupMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="groupName">The group name, from which the connection will leave.</param>
        public LeaveGroupMessage(string connectionId, string groupName)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
        }
    }

    public class JoinGroupWithAckMessage : JoinGroupMessage, IAckableMessage
    {
        /// <summary>
        /// Gets or sets the ack id.
        /// </summary>
        public string AckId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JoinGroupWithAckMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="groupName">The group name, to which the connection will join.</param>
        /// <param name="ackId">The ack Id</param>
        public JoinGroupWithAckMessage(string connectionId, string groupName, string ackId = null) : base(connectionId, groupName)
        {
            AckId = ackId;
        }
    }

    /// <summary>
    /// A leave-group message.
    /// </summary>
    public class LeaveGroupWithAckMessage : LeaveGroupMessage, IAckableMessage
    {
        /// <summary>
        /// Gets or sets the ack id.
        /// </summary>
        public string AckId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaveGroupWithAckMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="groupName">The group name, from which the connection will leave.</param>
        /// <param name="ackId">The ack Id</param>
        public LeaveGroupWithAckMessage(string connectionId, string groupName, string ackId = null) : base(connectionId, groupName)
        {
            AckId = ackId;
        }
    }

    public class GroupAckMessage : ServiceMessage, IAckableMessage
    {
        /// <summary>
        /// Gets or sets the ack id.
        /// </summary>
        public string AckId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JoinGroupWithAckMessage"/> class.
        /// </summary>
        /// <param name="ackId">The ack Id</param>
        public GroupAckMessage(string ackId)
        {
            AckId = ackId;
        }
    }
}
