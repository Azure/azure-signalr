// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Common
{
    /// <summary>
    /// Diagnostic settings for Azure SignalR service
    /// </summary>
    public class DiagnosticLog
    {
        /// <summary>
        /// Decides whether the server accept a client claim itself is a tracking client.
        /// </summary>
        public bool AcceptTrackingClient;
        /// <summary>
        /// Defines the log type of the diagnostic logs.
        /// </summary>
        public LogType LogType;
    }
}
