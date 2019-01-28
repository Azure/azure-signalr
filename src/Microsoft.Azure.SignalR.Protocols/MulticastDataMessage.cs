// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// Base class for multicast data messages between Azure SignalR Service and SDK.
    /// </summary>
    public abstract class MulticastDataMessage : ServiceMessage
    {
        protected MulticastDataMessage(IDictionary<string, ReadOnlyMemory<byte>> payloads)
        {
            Payloads = payloads;
        }

        /// <summary>
        /// Gets or sets the payload dictionary which contains binary payload of multiple protocols.
        /// </summary>
        public IDictionary<string, ReadOnlyMemory<byte>> Payloads { get; set; }
    }

    /// <summary>
    /// A data message which will be sent to multiple connections.
    /// </summary>
    public class MultiConnectionDataMessage : MulticastDataMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiConnectionDataMessage"/> class.
        /// </summary>
        /// <param name="connectionList">The list of connection Ids.</param>
        /// <param name="payloads">The payload dictionary which contains binary payload of multiple protocols.</param>
        public MultiConnectionDataMessage(IReadOnlyList<string> connectionList,
            IDictionary<string, ReadOnlyMemory<byte>> payloads) : base(payloads)
        {
            ConnectionList = connectionList;
        }

        /// <summary>
        /// Gets or sets the list of connections which will receive this message.
        /// </summary>
        public IReadOnlyList<string> ConnectionList { get; set; }
    }

    /// <summary>
    /// A data message which will be sent to a user.
    /// </summary>
    public class UserDataMessage : MulticastDataMessage
    {
        /// <summary>
        /// Gets or sets the user Id.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDataMessage"/> class.
        /// </summary>
        /// <param name="userId">The user Id.</param>
        /// <param name="payloads">The payload dictionary which contains binary payload of multiple protocols.</param>
        public UserDataMessage(string userId, IDictionary<string, ReadOnlyMemory<byte>> payloads) : base(payloads)
        {
            UserId = userId;
        }
    }

    /// <summary>
    /// A data message which will be sent to multiple users.
    /// </summary>
    public class MultiUserDataMessage : MulticastDataMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiUserDataMessage"/> class.
        /// </summary>
        /// <param name="userList">The list of user Ids.</param>
        /// <param name="payloads">The payload dictionary which contains binary payload of multiple protocols.</param>
        public MultiUserDataMessage(IReadOnlyList<string> userList, IDictionary<string, ReadOnlyMemory<byte>> payloads) : base(payloads)
        {
            UserList = userList;
        }

        /// <summary>
        /// Gets or sets the list of user Ids.
        /// </summary>
        public IReadOnlyList<string> UserList { get; set; }
    }

    /// <summary>
    /// A data message which will be broadcasted.
    /// </summary>
    public class BroadcastDataMessage : MulticastDataMessage
    {
        /// <summary>
        /// Gets or sets the list of excluded connection Ids.
        /// </summary>
        public IReadOnlyList<string> ExcludedList { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BroadcastDataMessage"/> class.
        /// </summary>
        /// <param name="payloads">The payload dictionary which contains binary payload of multiple protocols.</param>
        public BroadcastDataMessage(IDictionary<string, ReadOnlyMemory<byte>> payloads) : this(null, payloads)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BroadcastDataMessage"/> class.
        /// </summary>
        /// <param name="excludedList">The list of excluded connection Ids.</param>
        /// <param name="payloads">The payload dictionary which contains binary payload of multiple protocols.</param>
        public BroadcastDataMessage(IReadOnlyList<string> excludedList, IDictionary<string, ReadOnlyMemory<byte>> payloads) : base(payloads)
        {
            ExcludedList = excludedList;
        }
    }

    /// <summary>
    /// A data message which will be broadcasted within a group.
    /// </summary>
    public class GroupBroadcastDataMessage : MulticastDataMessage
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Gets or sets the list of excluded connection Ids.
        /// </summary>
        public IReadOnlyList<string> ExcludedList { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupBroadcastDataMessage"/> class.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="payloads">The payload dictionary which contains binary payload of multiple protocols.</param>
        public GroupBroadcastDataMessage(string groupName, IDictionary<string, ReadOnlyMemory<byte>> payloads)
            : this(groupName, null, payloads)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupBroadcastDataMessage"/> class.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="excludedList">The list of excluded connection Ids.</param>
        /// <param name="payloads">The payload dictionary which contains binary payload of multiple protocols.</param>
        public GroupBroadcastDataMessage(string groupName, IReadOnlyList<string> excludedList, IDictionary<string, ReadOnlyMemory<byte>> payloads)
            : base(payloads)
        {
            GroupName = groupName;
            ExcludedList = excludedList;
        }
    }

    /// <summary>
    /// A data message which will be broadcasted within multiple groups.
    /// </summary>
    public class MultiGroupBroadcastDataMessage : MulticastDataMessage
    {
        /// <summary>
        /// Gets or sets the list of group names.
        /// </summary>
        public IReadOnlyList<string> GroupList { get; set; }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="MultiGroupBroadcastDataMessage"/> class.
        /// </summary>
        /// <param name="groupList">The list of group names.</param>
        /// <param name="payloads">The payload dictionary which contains binary payload of multiple protocols.</param>
        public MultiGroupBroadcastDataMessage(IReadOnlyList<string> groupList, IDictionary<string, ReadOnlyMemory<byte>> payloads) : base(payloads)
        {
            GroupList = groupList;
        }
    }

    /// <summary>
    /// A data message which will be broadcasted within a group.
    /// </summary>
    public class GroupBroadcastDataWithAckMessage : MulticastDataMessage, IAckableMessage
    {
        /// <summary>
        /// Gets or sets the group name.
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// Gets or sets the list of excluded connection Ids.
        /// </summary>
        public IReadOnlyList<string> ExcludedList { get; set; }

        /// <summary>
        /// Gets or sets the ack id.
        /// </summary>
        public string AckId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Azure.SignalR.Protocol.GroupBroadcastDataWithAckMessage"/> class.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="payloads">The payload dictionary which contains binary payload of multiple protocols.</param>
        /// <param name="ackId"></param>
        public GroupBroadcastDataWithAckMessage(string groupName, IDictionary<string, ReadOnlyMemory<byte>> payloads, string ackId = null)
            : this(groupName, null, payloads, ackId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.Azure.SignalR.Protocol.GroupBroadcastDataWithAckMessage"/> class.
        /// </summary>
        /// <param name="groupName">The group name.</param>
        /// <param name="excludedList">The list of excluded connection Ids.</param>
        /// <param name="payloads">The payload dictionary which contains binary payload of multiple protocols.</param>
        /// <param name="ackId">The ack Id</param>
        public GroupBroadcastDataWithAckMessage(string groupName, IReadOnlyList<string> excludedList, IDictionary<string, ReadOnlyMemory<byte>> payloads, string ackId = null)
            : base(payloads)
        {
            GroupName = groupName;
            ExcludedList = excludedList;
            AckId = ackId;
        }
    }
}
