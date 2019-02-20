// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Transport type to the Azure SignalR Service.
    /// </summary>
    public enum ServiceTransportType
    {
        /// <summary>
        /// The SDK will call REST API to send each message to Azure SignalR Serivce.
        /// </summary>
        Transient,
        
        /// <summary>
        /// The SDK will establish one or more Websockets connection(s) to send all messages in the connection(s) to Azure SignalR Serivce.
        /// </summary>
        Persistent
    }
}