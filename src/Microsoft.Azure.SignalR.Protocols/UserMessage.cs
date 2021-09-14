// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Protocol
{
    /// <summary>
    /// Close connections for a user
    /// </summary>
    public class CloseUserConnectionsWithAckMessage : ExtensibleServiceMessage, IAckableMessage, IMessageWithTracingId
    {
        /// <summary>
        /// Gets or sets the user ID.
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the reason closing the connections
        /// </summary>
        public string Reason { get; set; }

        /// <summary>
        /// Gets or sets the ack id.
        /// </summary>
        public int AckId { get; set; }

        /// <summary>
        /// Gets or sets the tracing Id
        /// </summary>
        public ulong? TracingId { get; set; }

        /// <summary>
        /// Gets or sets the list of excluded connection Ids.
        /// </summary>
        public IReadOnlyList<string> ExcludedList { get; set; }

        public CloseUserConnectionsWithAckMessage(string userId, int ackId)
        {
            UserId = userId;
            AckId = ackId;
        }
    }
}
