// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    public class EndpointStat
    {
        /// <summary>
        /// <see cref="ServiceEndpoint" /> globally concurrent client connection count
        /// </summary>
        public int ClientCount { get; internal set; }
    }
}
