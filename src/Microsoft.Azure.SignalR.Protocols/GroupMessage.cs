// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Protocol
{
    public class JoinGroupMessage : ServiceMessage
    {
        public string ConnectionId { get; set; }
        public string GroupName { get; set; }

        public JoinGroupMessage(string connectionId, string groupName)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
        }
    }

    public class LeaveGroupMessage : ServiceMessage
    {
        public string ConnectionId { get; set; }
        public string GroupName { get; set; }

        public LeaveGroupMessage(string connectionId, string groupName)
        {
            ConnectionId = connectionId;
            GroupName = groupName;
        }
    }
}
