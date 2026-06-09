// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR;

/// <summary>
/// Specifies the mode for server sticky, when client is always routed to the server which it first /negotiate with, we call it "server sticky mode".
/// </summary>
public enum ServerStickyMode
{
    /// <summary>
    /// We the server sticky mode is disabled, it picks the server connection by some algorithm
    /// In general, local server connection first
    /// least client connections routed server connection first
    /// </summary>
    Disabled = 0,

    ///// <summary>
    ///// We will try to find the server it /neogitate with from local, if that server is connected to this runtime instance, we choose that server
    ///// Otherwise, we fallback to local existed server
    ///// </summary>
    Preferred = 1,

    /// <summary>
    /// We will try to find the server it /negotiate with from both local and global route table, it the server is not connected, throw,
    /// If it is globally routed, this request will be always globally routed
    /// </summary>
    Required = 2,
}
