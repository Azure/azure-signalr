// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR;

#nullable enable

internal interface IClientConnection
{
    /// <summary>
    /// The connection id.
    /// </summary>
    string ConnectionId { get; }

    /// <summary>
    /// The instance id.
    /// </summary>
    string InstanceId { get; }

    /// <summary>
    /// The connection protocol being used.
    /// JSON / MesssagePack
    /// </summary>
    string HubProtocol { get; }

    /// <summary>
    /// The server connection associated with this client connection.
    /// </summary>
    IServiceConnection? ServiceConnection { get; }
}
