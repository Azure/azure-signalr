// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    public class EndpointStat
    {
        /// <summary>
        /// <see cref="ServiceEndpoint" /> global concurrent client connection count
        /// </summary>
        public int ClientConnectionCount { get; internal set; }
    }
}
