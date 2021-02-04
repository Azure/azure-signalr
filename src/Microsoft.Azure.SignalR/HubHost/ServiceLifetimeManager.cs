// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
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
                var message = CreateMessage(connectionId, methodName, args, serviceConnectionContext);
                var messageWithTracingId = (IMessageWithTracingId)message;
                try
                {
                    // Write directly to this connection
                    await serviceConnectionContext.ServiceConnection.WriteAsync(message);

                    if (messageWithTracingId.TracingId != null)
                    {
                        MessageLog.SucceededToSendMessage(Logger, messageWithTracingId);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    MessageLog.FailedToSendMessage(Logger, messageWithTracingId, ex);
                    throw;
                }
            }

            await base.SendConnectionAsync(connectionId, methodName, args, cancellationToken);
        }

        private ServiceMessage CreateMessage(string connectionId, string methodName, object[] args, ClientConnectionContext serviceConnectionContext)
        {
            IDictionary<string, ReadOnlyMemory<byte>> payloads;
            if (serviceConnectionContext.Protocol != null)
            {
                payloads = new Dictionary<string, ReadOnlyMemory<byte>>()
                {
                    { serviceConnectionContext.Protocol, SerializeProtocol(serviceConnectionContext.Protocol, methodName, args) }
                };
            }
            else
            {
                payloads = SerializeAllProtocols(methodName, args);
            }

            // don't use ConnectionDataMessage here, since handshake message is also wrapped into ConnectionDataMessage.
            // otherwise it may cause the handshake failure due to hub invocation message is sent to client before handshake message, when there's high preasure on server.
            // do use ConnectionDataMessage when the message is sent from client.
            var message = new MultiConnectionDataMessage(new[] { connectionId }, payloads).WithTracingId();
            if (message.TracingId != null)
            {
                MessageLog.StartToSendMessageToConnections(Logger, message);
            }
            return message;
        }
    }
}
