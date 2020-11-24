// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Configuarable options for negotiation response options for redirecting client to SignalR service.
    /// </summary>
    public class NegotiationResponseOptions
    {
        /// <summary>
        /// Gets or sets the flag whether the negotation response contains diagnostic client marker.
        /// </summary>
        public bool IsDiagnosticClient { get; set; } = false;

        /// <summary>
        /// The interval used by the server to timeout incoming handshake requests in SignalR service by clients. The default timeout is 15 seconds. The valid timeout is between 1 second and 30 seconds.
        /// </summary>
        public TimeSpan? HandshakeTimeout { get; set; } = null;
    }
}
