// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class StrongServiceConnectionContainer : ServiceConnectionContainerBase
    {
        public StrongServiceConnectionContainer(
            IServiceConnectionFactory serviceConnectionFactory,
            int fixedConnectionCount,
            HubServiceEndpoint endpoint,
            ILogger logger) : base(serviceConnectionFactory, fixedConnectionCount, endpoint, logger: logger)
        {
        }

        public override async Task HandlePingAsync(PingMessage pingMessage)
        {
            await base.HandlePingAsync(pingMessage);
            if (ReadyForNewConnections)
            {
                if (RuntimeServicePingMessage.TryGetRebalance(pingMessage, out var target) && !string.IsNullOrEmpty(target))
                {
                    var connection = CreateServiceConnectionCore(ServiceConnectionType.OnDemand);
                    AddOnDemandConnection(connection);
                    await StartCoreAsync(connection, target);
                }
            }
        }
    }
}
