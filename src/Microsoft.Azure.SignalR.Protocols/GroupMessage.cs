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

    /// <summary>
    /// A user-join-group message.
    /// </summary>
    public class UserJoinGroupMessage : ServiceMessage
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
        /// Initializes a new instance of the <see cref="UserJoinGroupMessage"/> class.
        /// </summary>
        /// <param name="userId">The user Id.</param>
        /// <param name="groupName">The group name, to which the user will join.</param>
        public UserJoinGroupMessage(string userId, string groupName)
        {
            UserId = userId;
            GroupName = groupName;
        }
    }

    /// <summary>
    /// A user-leave-group message.
    /// </summary>
    public class UserLeaveGroupMessage : ServiceMessage
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
        /// Initializes a new instance of the <see cref="UserLeaveGroupMessage"/> class.
        /// </summary>
        /// <param name="userId">The user Id.</param>
        /// <param name="groupName">The group name, from which the user will leave.</param>
        public UserLeaveGroupMessage(string userId, string groupName)
        {
            UserId = userId;
            GroupName = groupName;
        }
    }
}
