// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    public class EndpointMetrics
    {
        /// <summary>
        /// <see cref="ServiceEndpoint" /> total concurrent connected client connection count on all hubs.
        /// </summary>
        public int ClientConnectionCount { get; internal set; }

        /// <summary>
        /// <see cref="ServiceEndpoint" /> total concurrent connected server connection count on all hubs.
        /// </summary>
        public int ServerConnectionCount { get; internal set; }

        /// <summary>
        /// <see cref="ServiceEndpoint" /> connection quota for this instance, including client and server connections. 
        /// </summary>
        public int ConnectionCapacity { get; internal set; }
    }
}