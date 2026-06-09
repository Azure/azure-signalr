// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR;

internal enum ServiceConnectionType
{
    /// <summary>
    /// 0, Default, it can carry clients, service runtime should always accept this kind of connection
    /// </summary>
    Default = 0,
    /// <summary>
    /// 1, OnDemand, creating when service requested more connections, it can carry clients, but it may be rejected by service runtime.
    /// </summary>
    OnDemand = 1,
    /// <summary>
    /// 2, Weak, it can not carry clients, but it can send message
    /// </summary>
    Weak = 2,
}
