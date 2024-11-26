// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// Base class of check-with-ack messages between Azure SignalR Service and SDK.
    /// </summary>
    public abstract class CheckWithAckMessage : ExtensibleServiceMessage, IAckableMessage, IMessageWithTracingId
    {
        /// <summary>
        /// Gets or sets the ack id.
        /// </summary>
        public int AckId { get; set; }

        /// <summary>
        /// Gets or sets the tracing Id
        /// </summary>
        public ulong? TracingId { get; set; }

        protected CheckWithAckMessage(int ackId, ulong? tracingId)
        {
            AckId = ackId;
            TracingId = tracingId;
        }
    }

    /// <summary>
    /// A waiting for ack check-user-in-group message.
    /// </summary>
    public class CheckUserInGroupWithAckMessage : CheckWithAckMessage, IPartitionableMessage
    {
        /// <summary>
        /// Gets or sets the user Id.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; }
        public byte PartitionKey => GeneratePartitionKey(GroupName);

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckUserInGroupWithAckMessage"/> class.
        /// </summary>
        /// <param name="userId">The user Id.</param>
        /// <param name="groupName">The group name to check whether the user is in or not.</param>
        /// <param name="ackId">The ack Id.</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public CheckUserInGroupWithAckMessage(string userId, string groupName, int ackId = 0, ulong? tracingId = null) : base(ackId, tracingId)
        {
            UserId = userId;
            GroupName = groupName;
        }
    }

    /// <summary>
    /// A waiting for ack check-any-connection-in-group message.
    /// </summary>
    public class CheckGroupExistenceWithAckMessage : CheckWithAckMessage, IPartitionableMessage
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; }

        public byte PartitionKey => GeneratePartitionKey(GroupName);

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckGroupExistenceWithAckMessage"/> class.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="ackId">The ack Id</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public CheckGroupExistenceWithAckMessage(string groupName, int ackId = 0, ulong? tracingId = null) : base(ackId, tracingId)
        {
            GroupName = groupName;
        }
    }

    /// <summary>
    /// A waiting for ack check-connection-existence message.
    /// </summary>
    public class CheckConnectionExistenceWithAckMessage : CheckWithAckMessage
    {
        /// <summary>
        /// Gets or sets the connection Id.
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckConnectionExistenceWithAckMessage"/> class.
        /// </summary>
        /// <param name="connectionId">The connection Id.</param>
        /// <param name="ackId">The ack Id</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public CheckConnectionExistenceWithAckMessage(string connectionId, int ackId = 0, ulong? tracingId = null) : base(ackId, tracingId)
        {
            ConnectionId = connectionId;
        }
    }

    /// <summary>
    /// A waiting for ack check-user-existence message.
    /// </summary>
    public class CheckUserExistenceWithAckMessage : CheckWithAckMessage, IPartitionableMessage
    {
        /// <summary>
        /// Gets or sets the user Id.
        /// </summary>
        public string UserId { get; set; }
        public byte PartitionKey => GeneratePartitionKey(UserId);

        /// <summary>
        /// Initializes a new instance of the <see cref="CheckUserExistenceWithAckMessage"/> class.
        /// </summary>
        /// <param name="userId">The user Id.</param>
        /// <param name="ackId">The ack Id</param>
        /// <param name="tracingId">The tracing Id of the message.</param>
        public CheckUserExistenceWithAckMessage(string userId, int ackId = 0, ulong? tracingId = null) : base(ackId, tracingId)
        {
            UserId = userId;
        }
    }
}
