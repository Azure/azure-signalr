// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal sealed class TestBaseServiceConnectionContainer(
    List<IServiceConnection> serviceConnections,
    HubServiceEndpoint endpoint = null,
    ILogger logger = null) : ServiceConnectionContainerBase(null, 0, endpoint, serviceConnections, logger: logger ?? NullLogger.Instance)
{
    public override Task HandlePingAsync(PingMessage pingMessage)
    {
        return Task.CompletedTask;
    }
}
