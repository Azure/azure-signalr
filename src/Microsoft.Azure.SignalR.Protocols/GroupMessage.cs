// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// A join-group message.
    /// </summary>
    public class JoinGroupMessage : ExtensibleServiceMessage, IMessageWithTracingId
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
        /// Gets or sets the tracing Id
        /// </summary>
        public string TracingId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JoinGroupMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="groupName">The group name, to which the connection will join.</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public JoinGroupMessage(string connectionId, string groupName, string tracingId = null)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
            TracingId = tracingId;
        }
    }

    /// <summary>
    /// A leave-group message.
    /// </summary>
    public class LeaveGroupMessage : ExtensibleServiceMessage, IMessageWithTracingId
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
        /// Gets or sets the tracing Id
        /// </summary>
        public string TracingId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaveGroupMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="groupName">The group name, from which the connection will leave.</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public LeaveGroupMessage(string connectionId, string groupName, string tracingId = null)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
            TracingId = tracingId;
        }
    }

    /// <summary>
    /// A user-join-group message.
    /// </summary>
    public class UserJoinGroupMessage : ExtensibleServiceMessage, IMessageWithTracingId, IHasTtl
    {
        /// <summary>
        /// Gets or sets the user Id.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Gets or sets the tracing Id
        /// </summary>
        public string TracingId { get; set; }

        /// <summary>
        /// Time to live for the user in the group.
        /// </summary>
        public int? Ttl { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserJoinGroupMessage"/> class.
        /// </summary>
        /// <param name="userId">The user Id.</param>
        /// <param name="groupName">The group name, to which the user will join.</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public UserJoinGroupMessage(string userId, string groupName, string tracingId = null)
        {
            UserId = userId;
            GroupName = groupName;
            TracingId = tracingId;
        }
    }

    /// <summary>
    /// A user-leave-group message.
    /// </summary>
    public class UserLeaveGroupMessage : ExtensibleServiceMessage, IMessageWithTracingId
    {
        /// <summary>
        /// Gets or sets the user Id.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; }
        
        /// <summary>
        /// Gets or sets the tracing Id
        /// </summary>
        public string TracingId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserLeaveGroupMessage"/> class.
        /// </summary>
        /// <param name="userId">The user Id.</param>
        /// <param name="groupName">The group name, from which the user will leave.</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public UserLeaveGroupMessage(string userId, string groupName, string tracingId = null)
        {
            UserId = userId;
            GroupName = groupName;
            TracingId = tracingId;
        }
    }

    /// <summary>
    /// A waiting for ack join-group message.
    /// </summary>
    public class JoinGroupWithAckMessage : ExtensibleServiceMessage, IAckableMessage, IMessageWithTracingId
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
        /// Gets or sets the ack id.
        /// </summary>
        public int AckId { get; set; }

        /// <summary>
        /// Gets or sets the tracing Id
        /// </summary>
        public string TracingId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="JoinGroupWithAckMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="groupName">The group name, to which the connection will join.</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public JoinGroupWithAckMessage(string connectionId, string groupName, string tracingId = null): this(connectionId, groupName, 0, tracingId)
        {
            TracingId = tracingId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JoinGroupWithAckMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="groupName">The group name, to which the connection will join.</param>
        /// <param name="ackId">The ack Id</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public JoinGroupWithAckMessage(string connectionId, string groupName, int ackId, string tracingId = null)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
            AckId = ackId;
            TracingId = tracingId;
        }
    }

    /// <summary>
    /// A waiting for ack leave-group message.
    /// </summary>
    public class LeaveGroupWithAckMessage : ExtensibleServiceMessage, IAckableMessage, IMessageWithTracingId
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
        /// Gets or sets the ack id.
        /// </summary>
        public int AckId { get; set; }

        /// <summary>
        /// Gets or sets the tracing Id
        /// </summary>
        public string TracingId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaveGroupWithAckMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="groupName">The group name, from which the connection will leave.</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public LeaveGroupWithAckMessage(string connectionId, string groupName, string tracingId = null): this(connectionId, groupName, 0, tracingId)
        {
            TracingId = tracingId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaveGroupWithAckMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="groupName">The group name, from which the connection will leave.</param>
        /// <param name="ackId">The ack Id</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public LeaveGroupWithAckMessage(string connectionId, string groupName, int ackId, string tracingId = null)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
            AckId = ackId;
            TracingId = tracingId;
        }
    }
}
