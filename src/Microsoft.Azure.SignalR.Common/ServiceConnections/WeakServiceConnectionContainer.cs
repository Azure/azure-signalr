// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

internal class WeakServiceConnectionContainer : ServiceConnectionContainerBase
{
    protected override ServiceConnectionType InitialConnectionType => ServiceConnectionType.Weak;

    public WeakServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
        int fixedConnectionCount, HubServiceEndpoint endpoint, ILogger logger)
        : base(serviceConnectionFactory, fixedConnectionCount, endpoint, logger: logger)
    {
    }

    public override Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token)
    {
        return Task.CompletedTask;
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, ServiceEndpoint, string, Exception> _ignoreSendingMessageToInactiveEndpoint =
            LoggerMessage.Define<string, ServiceEndpoint, string>(LogLevel.Debug, new EventId(1, "IgnoreSendingMessageToInactiveEndpoint"), "Message {type} sending to {endpoint} for hub {hub} is ignored because the endpoint is inactive.");

        public static void IgnoreSendingMessageToInactiveEndpoint(ILogger logger, Type messageType, HubServiceEndpoint endpoint)
        {
            _ignoreSendingMessageToInactiveEndpoint(logger, messageType.Name, endpoint, endpoint.Hub, null);
        }
    }
}
