// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

internal class StrongServiceConnectionContainer : ServiceConnectionContainerBase
{
    private readonly int? _maxConnectionCount;

    public StrongServiceConnectionContainer(
        IServiceConnectionFactory serviceConnectionFactory,
        int fixedConnectionCount,
        int? maxConnectionCount,
        HubServiceEndpoint endpoint,
        ILogger logger) : base(serviceConnectionFactory, fixedConnectionCount, endpoint, logger: logger)
    {
        _maxConnectionCount = maxConnectionCount.HasValue ? (maxConnectionCount.Value > fixedConnectionCount ? maxConnectionCount.Value : fixedConnectionCount) : null;
    }

    public override async Task HandlePingAsync(PingMessage pingMessage)
    {
        await base.HandlePingAsync(pingMessage);
        if (RuntimeServicePingMessage.TryGetRebalance(pingMessage, out var target) && !string.IsNullOrEmpty(target)
            && (_maxConnectionCount == null || ServiceConnections.Count < _maxConnectionCount))
        {
            var connection = CreateServiceConnectionCore(ServiceConnectionType.OnDemand);
            AddOnDemandConnection(connection);
            await StartCoreAsync(connection, target);
        }
    }
}
