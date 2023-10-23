// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Protocol
{
    public class ConnectionWithAckMessage
    {
        public class MigrateConnectionWithAckMessage : ExtensibleServiceMessage, IAckableMessage
        {
            /// <summary>
            /// Gets or sets the ack Id.
            /// </summary>
            public int AckId { get; set; }

            /// <summary>
            /// Gets or sets the connection Id.
            /// </summary>
            public string ConnectionId { get; set; }
        }
    }
}
