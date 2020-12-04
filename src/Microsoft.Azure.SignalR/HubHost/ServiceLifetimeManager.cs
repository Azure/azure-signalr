﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceLifetimeManager<THub> : ServiceLifetimeManagerBase<THub> where THub : Hub
    {
        private const string MarkerNotConfiguredError =
            "'AddAzureSignalR(...)' was called without a matching call to 'IApplicationBuilder.UseAzureSignalR(...)'.";

        private readonly IClientConnectionManager _clientConnectionManager;

        public ServiceLifetimeManager(
            IServiceConnectionManager<THub> serviceConnectionManager,
            IClientConnectionManager clientConnectionManager,
            IHubProtocolResolver protocolResolver,
            ILogger<ServiceLifetimeManager<THub>> logger,
            AzureSignalRMarkerService marker,
            IOptions<HubOptions> globalHubOptions,
            IOptions<HubOptions<THub>> hubOptions,
            IBlazorDetector blazorDetector)
            : base(
                  serviceConnectionManager,
                  protocolResolver,
                  globalHubOptions,
                  hubOptions, logger)
        {
            // after core 3.0 UseAzureSignalR() is not required.
#if NETSTANDARD2_0
            if (!marker.IsConfigured)
            {
                throw new InvalidOperationException(MarkerNotConfiguredError);
            }
#endif
            _clientConnectionManager = clientConnectionManager;

            if (hubOptions.Value.SupportedProtocols != null && hubOptions.Value.SupportedProtocols.Any(x => x.Equals(Constants.Protocol.BlazorPack, StringComparison.OrdinalIgnoreCase)))
            {
                blazorDetector?.TrySetBlazor(typeof(THub).Name, true);
            }
        }

        public override async Task SendConnectionAsync(string connectionId, string methodName, object[] args, CancellationToken cancellationToken = default)
        {
            if (IsInvalidArgument(connectionId))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(connectionId));
            }

            if (IsInvalidArgument(methodName))
            {
                throw new ArgumentException(NullOrEmptyStringErrorMessage, nameof(methodName));
            }

            if (_clientConnectionManager.ClientConnections.TryGetValue(connectionId, out var serviceConnectionContext))
            {
                if (serviceConnectionContext.Protocol != null)
                {
                    var message = new ConnectionDataMessage(connectionId, SerializeProtocol(serviceConnectionContext.Protocol, methodName, args)).WithTracingId();
                    if (message.TracingId != null)
                    {
                        MessageLog.StartToSendMessageToConnection(Logger, message);
                    }

                    try
                    {
                        // Write directly to this connection
                        await serviceConnectionContext.ServiceConnection.WriteAsync(message);

                        if (message.TracingId != null)
                        {
                            MessageLog.SucceededToSendMessage(Logger, message);
                        }
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageLog.FailedToSendMessage(Logger, message, ex);
                        throw;
                    }
                }
                else
                {
                    var message = new MultiConnectionDataMessage(new[] { connectionId }, SerializeAllProtocols(methodName, args)).WithTracingId();
                    if (message.TracingId != null)
                    {
                        MessageLog.StartToSendMessageToConnections(Logger, message);
                    }

                    try
                    {
                        // Write directly to this connection
                        await serviceConnectionContext.ServiceConnection.WriteAsync(message);

                        if (message.TracingId != null)
                        {
                            MessageLog.SucceededToSendMessage(Logger, message);
                        }
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageLog.FailedToSendMessage(Logger, message, ex);
                        throw;
                    }
                }
            }

            await base.SendConnectionAsync(connectionId, methodName, args, cancellationToken);
        }
    }
}
