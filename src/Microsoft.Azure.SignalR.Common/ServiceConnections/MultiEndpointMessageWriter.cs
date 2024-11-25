// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

/// <summary>
/// A service connection container which sends message to multiple service endpoints.
/// </summary>
internal class MultiEndpointMessageWriter : IServiceMessageWriter
{
    private readonly ILogger _logger;

    internal HubServiceEndpoint[] TargetEndpoints { get; }

    public MultiEndpointMessageWriter(IReadOnlyCollection<ServiceEndpoint> targetEndpoints, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<MultiEndpointMessageWriter>();
        var normalized = new List<HubServiceEndpoint>();
        if (targetEndpoints != null)
        {
            foreach (var endpoint in targetEndpoints.Where(s => s != null))
            {
                var hubEndpoint = endpoint as HubServiceEndpoint;
                // it is possible that the endpoint is not a valid HubServiceEndpoint since it can be changed by the router
                if (hubEndpoint == null || hubEndpoint.ConnectionContainer == null)
                {
                    Log.EndpointNotExists(_logger, endpoint.ToString());
                }
                else
                {
                    normalized.Add(hubEndpoint);
                }
            }
        }

        TargetEndpoints = normalized.ToArray();
    }

    public Task ConnectionInitializedTask => Task.WhenAll(TargetEndpoints.Select(e => e.ConnectionContainer.ConnectionInitializedTask));

    public Task WriteAsync(ServiceMessage serviceMessage)
    {
        return WriteMultiEndpointMessageAsync(serviceMessage, connection => connection.WriteAsync(serviceMessage));
    }

    public Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
    {
        if (serviceMessage is CheckConnectionExistenceWithAckMessage 
            || serviceMessage is JoinGroupWithAckMessage 
            || serviceMessage is LeaveGroupWithAckMessage)
        {
            return WriteSingleResultAckableMessage(serviceMessage, cancellationToken);
        }
        else
        {
            return WriteMultiResultAckableMessage(serviceMessage, cancellationToken);
        }
    }

    /// <summary>
    /// For user or group related operations, different endpoints might return different results
    /// Strategy:
    /// Always wait until all endpoints return or throw
    /// * When any endpoint throws, throw
    /// * When all endpoints return false, return false
    /// * When any endpoint returns true, return true
    /// </summary>
    /// <param name="serviceMessage"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<bool> WriteMultiResultAckableMessage(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var bag = new ConcurrentBag<bool>();
        await WriteMultiEndpointMessageAsync(serviceMessage, async connection =>
        {
            bag.Add(await connection.WriteAckableMessageAsync(serviceMessage.Clone(), cancellationToken));
        });

        return bag.Any(i => i);
    }

    /// <summary>
    /// For connection related operations, since connectionId is globally unique, only one endpoint can have the connection
    /// Strategy:
    /// Don't need to wait until all endpoints return or throw
    /// * Whenever any endpoint returns true: return true
    /// * When any endpoint throws throw
    /// * When all endpoints return false, return false
    /// </summary>
    /// <param name="serviceMessage"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<bool> WriteSingleResultAckableMessage(ServiceMessage serviceMessage, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var writeMessageTask = WriteMultiEndpointMessageAsync(serviceMessage, async connection =>
        {
            var succeeded = await connection.WriteAckableMessageAsync(serviceMessage.Clone(), cancellationToken);
            if (succeeded)
            {
                tcs.TrySetResult(true);
            }
        });

        // we wait when tcs is set to true or all the tasks return
        var task = await Task.WhenAny(tcs.Task, writeMessageTask);

        // tcs is either already set as true or should be false now
        tcs.TrySetResult(false);

        if (tcs.Task.Result)
        {
            return true;
        }

        // This will throw exceptions in tasks if exceptions exist
        await writeMessageTask;
        return false;
    }

    private async Task WriteMultiEndpointMessageAsync(ServiceMessage serviceMessage, Func<IServiceConnectionContainer, Task> inner)
    {
        if (TargetEndpoints.Length == 0)
        {
            Log.NoEndpointRouted(_logger, serviceMessage.GetType().Name);
            return;
        }

        if (TargetEndpoints.Length == 1)
        {
            await WriteSingleEndpointMessageAsync(TargetEndpoints[0], serviceMessage, inner);
            return;
        }

        var task = Task.WhenAll(TargetEndpoints.Select((endpoint) => WriteSingleEndpointMessageAsync(endpoint, serviceMessage, inner)));
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            // throw the aggregated exception instead
            throw task.Exception ?? ex;
        }
    }

    private async Task WriteSingleEndpointMessageAsync(HubServiceEndpoint endpoint, ServiceMessage serviceMessage, Func<IServiceConnectionContainer, Task> inner)
    {
        try
        {
            Log.RouteMessageToServiceEndpoint(_logger, serviceMessage, endpoint.ToString());
            await inner(endpoint.ConnectionContainer);
        }
        catch (ServiceConnectionNotActiveException)
        {
            // log and don't stop other endpoints
            Log.FailedWritingMessageToEndpoint(_logger, serviceMessage.GetType().Name, (serviceMessage as IMessageWithTracingId)?.TracingId, endpoint.ToString());
            throw new FailedWritingMessageToServiceException(endpoint.ServerEndpoint.AbsoluteUri);
        }
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
}