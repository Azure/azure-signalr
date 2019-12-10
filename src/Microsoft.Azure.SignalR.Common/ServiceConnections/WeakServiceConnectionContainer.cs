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
        private static readonly TimeSpan DefaultGetServiceStatusInterval = TimeSpan.FromSeconds(10);
        private static readonly long DefaultGetServiceStatusTicks = DefaultGetServiceStatusInterval.Seconds * Stopwatch.Frequency;
        private static readonly TimeSpan CheckTimeSpan = TimeSpan.FromMinutes(10);

        private readonly object _lock = new object();
        private int _inactiveCount;
        private DateTime? _firstInactiveTime;
        private long _lastSendTimestamp;

        // active ones are those whose client connections connected to the whole endpoint
        private volatile bool _active = true;

        private readonly TimerAwaitable _timer;

        protected override ServiceConnectionType InitialConnectionType => ServiceConnectionType.Weak;

        public WeakServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            int fixedConnectionCount, HubServiceEndpoint endpoint, ILogger logger)
            : base(serviceConnectionFactory, fixedConnectionCount, endpoint, logger: logger)
        {
            _timer = StartServiceStatusPingTimer();
        }

        public override Task HandlePingAsync(PingMessage pingMessage)
        {
            if (pingMessage.TryGetServiceStatusPingMessage(out var status))
            {
                _active = GetServiceStatus(status.IsActive, CheckWindow, CheckTimeSpan);
                Log.ReceivedServiceStatusPing(Logger, status.IsActive, Endpoint);
            }

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

        public override Task OfflineAsync()
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

        private TimerAwaitable StartServiceStatusPingTimer()
        {
            Log.StartingServiceStatusPingTimer(Logger, DefaultGetServiceStatusInterval);

            _lastSendTimestamp = Stopwatch.GetTimestamp();
            var timer = new TimerAwaitable(DefaultGetServiceStatusInterval, DefaultGetServiceStatusInterval);
            _ = ServiceStatusPingAsync(timer);

            return timer;
        }

        private async Task ServiceStatusPingAsync(TimerAwaitable timer)
        {
            using (timer)
            {
                timer.Start();

                while (await timer)
                {
                    try
                    {
                        // Check if last send time is longer than default keep-alive ticks and then send ping
                        if (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastSendTimestamp) > DefaultGetServiceStatusTicks)
                        {
                            await WriteAsync(ServiceStatusPingMessage.ActiveServicePingMessage);
                            Interlocked.Exchange(ref _lastSendTimestamp, Stopwatch.GetTimestamp());
                            Log.SentServiceStatusPing(Logger);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.FailedSendingServiceStatusPing(Logger, e);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
            }

            base.Dispose(disposing);
        }

        private static class Log
        {
            private static readonly Action<ILogger, double, Exception> _startingServiceStatusPingTimer =
                LoggerMessage.Define<double>(LogLevel.Debug, new EventId(0, "StartingServiceStatusPingTimer"), "Starting service status ping timer. Duration: {KeepAliveInterval:0.00}ms");

            private static readonly Action<ILogger, Exception> _sentServiceStatusPing =
                LoggerMessage.Define(LogLevel.Debug, new EventId(1, "SentServiceStatusPing"), "Sent a service status ping message to service.");

            private static readonly Action<ILogger, Exception> _failedSendingServiceStatusPing =
                LoggerMessage.Define(LogLevel.Warning, new EventId(2, "FailedSendingServiceStatusPing"), "Failed sending a service status ping message to service.");

            private static readonly Action<ILogger, string, ServiceEndpoint, string, Exception> _ignoreSendingMessageToInactiveEndpoint =
                LoggerMessage.Define<string, ServiceEndpoint, string>(LogLevel.Debug, new EventId(3, "IgnoreSendingMessageToInactiveEndpoint"), "Message {type} sending to {endpoint} for hub {hub} is ignored because the endpoint is inactive.");

            private static readonly Action<ILogger, bool, ServiceEndpoint, string, Exception> _receivedServiceStatusPing =
                LoggerMessage.Define<bool, ServiceEndpoint, string>(LogLevel.Debug, new EventId(4, "ReceivedServiceStatusPing"), "Received a service status active={isActive} from {endpoint} for hub {hub}.");

            public static void StartingServiceStatusPingTimer(ILogger logger, TimeSpan keepAliveInterval)
            {
                _startingServiceStatusPingTimer(logger, keepAliveInterval.TotalMilliseconds, null);
            }

            public static void SentServiceStatusPing(ILogger logger)
            {
                _sentServiceStatusPing(logger, null);
            }

            public static void FailedSendingServiceStatusPing(ILogger logger, Exception exception)
            {
                _failedSendingServiceStatusPing(logger, exception);
            }

            public static void IgnoreSendingMessageToInactiveEndpoint(ILogger logger, Type messageType, HubServiceEndpoint endpoint)
            {
                _ignoreSendingMessageToInactiveEndpoint(logger, messageType.Name, endpoint, endpoint.Hub, null);
            }

            public static void ReceivedServiceStatusPing(ILogger logger, bool isActive, HubServiceEndpoint endpoint)
            {
                _receivedServiceStatusPing(logger, isActive, endpoint, endpoint.Hub, null);
            }
        }
    }
}
