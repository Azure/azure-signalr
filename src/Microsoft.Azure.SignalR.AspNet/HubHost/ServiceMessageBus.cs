// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet;

internal class ServiceMessageBus : MessageBus
{
    private readonly IMessageParser _parser;

    private readonly IServiceConnectionManager _serviceConnectionManager;

    private readonly IClientConnectionManagerAspNet _clientConnectionManager;

    private readonly IAckHandler _ackHandler;

    private readonly ILogger<ServiceMessageBus> _logger;

    public ServiceMessageBus(IDependencyResolver resolver, ILogger<ServiceMessageBus> logger) : base(resolver)
    {
        // TODO: find a more decent way instead of DI, it can be easily overriden
        _serviceConnectionManager = resolver.Resolve<IServiceConnectionManager>() ?? throw new ArgumentNullException(nameof(IServiceConnectionManager));
        _clientConnectionManager = resolver.Resolve<IClientConnectionManagerAspNet>() ?? throw new ArgumentNullException(nameof(IClientConnectionManagerAspNet));
        _parser = resolver.Resolve<IMessageParser>() ?? throw new ArgumentNullException(nameof(IMessageParser));
        _ackHandler = resolver.Resolve<IAckHandler>() ?? throw new ArgumentNullException(nameof(IAckHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(ILogger<ServiceMessageBus>));
    }

    public override Task Publish(Message message)
    {
        var messages = _parser.GetMessages(message).ToList();
        if (messages.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (messages.Count == 1)
        {
            return ProcessMessage(messages[0]);
        }

        return Task.WhenAll(messages.Select(m => ProcessMessage(m)));
    }

    private async Task ProcessMessage(AppMessage message)
    {
        if (message is HubMessage hubMessage)
        {
            await WriteMessage(_serviceConnectionManager.WithHub(hubMessage.HubName), message);
            return;
        }

        await WriteMessage(_serviceConnectionManager, message);
    }

    private async Task WriteMessage(IServiceConnectionContainer connection, AppMessage appMessage)
    {
        var message = appMessage.Message;
        try
        {
            switch (message)
            {
                // For group related messages, make sure messages are written to the same partition
                case JoinGroupWithAckMessage joinGroupMessage:
                    try
                    {
                        await connection.WriteAckableMessageAsync(joinGroupMessage);
                    }
                    finally
                    {
                        _ackHandler.TriggerAck(appMessage.RawMessage.CommandId);
                    }
                    break;
                case LeaveGroupWithAckMessage leaveGroupMessage:
                    try
                    {
                        await connection.WriteAckableMessageAsync(leaveGroupMessage);
                    }
                    finally
                    {
                        _ackHandler.TriggerAck(appMessage.RawMessage.CommandId);
                    }
                    break;
                case ConnectionDataMessage connectionDataMessage:
                    var connectionId = connectionDataMessage.ConnectionId;
                    if (_clientConnectionManager.TryGetClientConnection(connectionId, out var clientConnection))
                    {
                        // If the client connection is connected to local server connection,
                        // send back directly from the established server connection
                        await clientConnection.ServiceConnection.WriteAsync(connectionDataMessage);
                    }
                    else
                    {
                        await connection.WriteAsync(message);
                    }
                    break;
                default:
                    await connection.WriteAsync(message);
                    break;
            }

            if (message is IMessageWithTracingId msg && msg.TracingId != null)
            {
                MessageLog.SucceededToSendMessage(_logger, msg);
            }
        }
        catch (Exception ex)
        {
            if (message is IMessageWithTracingId msg && msg.TracingId != null)
            {
                MessageLog.FailedToSendMessage(_logger, msg, ex);
            }
            throw;
        }
    }
}
