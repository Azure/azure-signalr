// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Common
{
    /// <summary>
    /// Log type of the diagnostic logs
    /// </summary>
    public enum LogType
    {
        /// <summary>
        /// Connectivity logs provide detailed information of connections
        /// </summary>
        Connectivity,
        /// <summary>
        /// Connectivity logs provide detailed information of messages
        /// </summary>
        Messaging
    }
}
