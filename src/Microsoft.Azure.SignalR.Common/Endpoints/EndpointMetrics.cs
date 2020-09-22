﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    public class EndpointMetrics
    {
        /// <summary>
        /// <see cref="ServiceEndpoint" /> total concurrent client connection count on all hubs.
        /// </summary>
        public int ClientConnectionCount { get; internal set; }
    }
}
