// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Common.ServiceConnections
{
    internal class WeakServiceConnectionContainer : ServiceConnectionContainerBase
    {
        private const int CheckWindow = 5;
        private static readonly TimeSpan CheckTimeSpan = TimeSpan.FromMinutes(10);

        private readonly object _lock = new object();
        private int _inactiveCount;
        private DateTime? _firstInactiveTime;

        // active ones are those whose client connections connected to the whole endpoint
        private volatile bool _active = true;

        protected override ServiceConnectionType InitialConnectionType => ServiceConnectionType.Weak;

        public WeakServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            int fixedConnectionCount, HubServiceEndpoint endpoint, ILogger logger)
            : base(serviceConnectionFactory, fixedConnectionCount, endpoint, logger: logger)
        {
        }

        public override Task HandlePingAsync(PingMessage pingMessage)
        {
            base.HandlePingAsync(pingMessage);
            var active = HasClients;
            _active = GetServiceStatus(active, CheckWindow, CheckTimeSpan);

            return Task.CompletedTask;
        }

        public override Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (!_active && !(serviceMessage is PingMessage))
            {
                // If the endpoint is inactive, there is no need to send messages to it
                Log.IgnoreSendingMessageToInactiveEndpoint(Logger, serviceMessage.GetType(), Endpoint);
                return Task.CompletedTask;
            }

            return base.WriteAsync(serviceMessage);
        }

        public override Task OfflineAsync(bool migratable)
        {
            return Task.CompletedTask;
        }

        internal bool GetServiceStatus(bool active, int checkWindow, TimeSpan checkTimeSpan)
        {
            lock (_lock)
            {
                if (active)
                {
                    _firstInactiveTime = null;
                    _inactiveCount = 0;
                    return true;
                }
                else
                {
                    if (_firstInactiveTime == null)
                    {
                        _firstInactiveTime = DateTime.UtcNow;
                    }

                    _inactiveCount++;

                    // Inactive it only when it checks over 5 times and elapsed for over 10 minutes
                    if (_inactiveCount >= checkWindow && DateTime.UtcNow - _firstInactiveTime >= checkTimeSpan)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
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
}
