// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// A service connection container which sends message to multiple service endpoints.
    /// </summary>
    internal class MultiEndpointMessageWriter : IMultiEndpointServiceConnectionContainer
    {
        private readonly ILogger _logger;
        internal IReadOnlyCollection<HubServiceEndpoint> TargetEndpoints { get; }

        public MultiEndpointMessageWriter(IEnumerable<HubServiceEndpoint> targetEndpoints, ILoggerFactory loggerFactory)
        {
            TargetEndpoints = targetEndpoints.ToList();
            _logger = loggerFactory.CreateLogger<MultiEndpointMessageWriter>();
        }

        public Task ConnectionInitializedTask => TargetEndpoints == null ? Task.CompletedTask : Task.WhenAll(TargetEndpoints.Select(e => e.ConnectionContainer.ConnectionInitializedTask));

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            return WriteMultiEndpointMessageAsync(serviceMessage, connection => connection.WriteAsync(serviceMessage));
        }

        public async Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
        {
            // If we have multiple endpoints, we should wait to one of the following conditions hit
            // 1. One endpoint responses "OK" state
            // 2. All the endpoints response failed state including "NotFound", "Timeout" and waiting response to timeout
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var writeMessageTask = WriteMultiEndpointMessageAsync(serviceMessage, async connection =>
            {
                var succeeded = await connection.WriteAckableMessageAsync(serviceMessage, cancellationToken);
                if (succeeded)
                {
                    tcs.TrySetResult(true);
                }
            });

            // If tcs.Task completes, one Endpoint responses "OK" state.
            var task = await Task.WhenAny(tcs.Task, writeMessageTask);

            // This will throw exceptions in tasks if exceptions exist
            await task;

            return tcs.Task.IsCompleted;
        }

        private Task WriteMultiEndpointMessageAsync(ServiceMessage serviceMessage, Func<IServiceConnectionContainer, Task> inner)
        {
            var routed = TargetEndpoints?
                .Select(endpoint =>
                {
                    var connection = endpoint?.ConnectionContainer;
                    if (connection == null)
                    {
                        Log.EndpointNotExists(_logger, endpoint.ToString());
                    }
                    return (e: endpoint, c: connection);
                })
                .Where(c => c.c != null)
                .Select(async s =>
                {
                    try
                    {
                        Log.RouteMessageToServiceEndpoint(_logger, serviceMessage, s.e.ToString());
                        await inner(s.c);
                    }
                    catch (ServiceConnectionNotActiveException)
                    {
                        // log and don't stop other endpoints
                        Log.FailedWritingMessageToEndpoint(_logger, serviceMessage.GetType().Name, (serviceMessage as IMessageWithTracingId)?.TracingId, s.e.ToString());
                    }
                }).ToArray();

            if (routed == null || routed.Length == 0)
            {
                // check if the router returns any endpoint
                Log.NoEndpointRouted(_logger, serviceMessage.GetType().Name);
                return Task.CompletedTask;
            }

            if (routed.Length == 1)
            {
                return routed[0];
            }

            return Task.WhenAll(routed);
        }

        public Task StartAsync() => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public void Dispose()
        {
        }

        internal static class Log
        {
            public const string FailedWritingMessageToEndpointTemplate = "{0} message {1} is not sent to endpoint {2} because all connections to this endpoint are offline.";

            private static readonly Action<ILogger, string, Exception> _endpointNotExists =
                LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, "EndpointNotExists"), "Endpoint {endpoint} from the router does not exists.");

            private static readonly Action<ILogger, string, Exception> _noEndpointRouted =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "NoEndpointRouted"), "Message {messageType} is not sent because no endpoint is returned from the endpoint router.");

            private static readonly Action<ILogger, string, ulong?, string, Exception> _failedWritingMessageToEndpoint =
                LoggerMessage.Define<string, ulong?, string>(LogLevel.Warning, new EventId(5, "FailedWritingMessageToEndpoint"), FailedWritingMessageToEndpointTemplate);

            private static readonly Action<ILogger, ulong?, string, Exception> _routeMessageToServiceEndpoint =
                LoggerMessage.Define<ulong?, string>(LogLevel.Information, new EventId(11, "RouteMessageToServiceEndpoint"), "Route message {tracingId} to service endpoint {endpoint}.");

            public static void RouteMessageToServiceEndpoint(ILogger logger, ServiceMessage message, string endpoint)
            {
                if (ServiceConnectionContainerScope.EnableMessageLog || ClientConnectionScope.IsDiagnosticClient)
                {
                    _routeMessageToServiceEndpoint(logger, (message as IMessageWithTracingId).TracingId, endpoint, null);
                }
            }

            public static void EndpointNotExists(ILogger logger, string endpoint)
            {
                _endpointNotExists(logger, endpoint, null);
            }

            public static void NoEndpointRouted(ILogger logger, string messageType)
            {
                _noEndpointRouted(logger, messageType, null);
            }

            public static void FailedWritingMessageToEndpoint(ILogger logger, string messageType, ulong? tracingId, string endpoint)
            {
                _failedWritingMessageToEndpoint(logger, messageType, tracingId, endpoint, null);
            }
        }

        #region Not supported method or properties

        public ServiceConnectionStatus Status => throw new NotSupportedException();

        public string ServersTag => throw new NotSupportedException();

        public bool HasClients => throw new NotSupportedException();

        public Task OfflineAsync(GracefulShutdownMode mode) => throw new NotSupportedException();

        public Task StartGetServersPing() => throw new NotSupportedException();

        public Task StopGetServersPing() => throw new NotSupportedException();

        #endregion Not supported method or properties
    }
}