// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    public class DefaultServiceEventHandler : IServiceEventHandler
    {
        private readonly ILogger _logger;

        public DefaultServiceEventHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<DefaultServiceEventHandler>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public Task HandleAsync(string connectionId, ServiceEventMessage message)
        {
            Log.ServiceEvent(_logger, connectionId, message);
            return Task.CompletedTask;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, string, string, string, string, Exception> _serviceEvent =
                LoggerMessage.Define<string, string, string, string, string>(
                    LogLevel.Information,
                    new EventId(1, "ServiceEvent"),
                    "{connectionId} recieved service event for {objectType}({objectId}) is {kind}, message:{message}");

            public static void ServiceEvent(ILogger logger, string connectionId, ServiceEventMessage message)
            {
                _serviceEvent(logger, connectionId, message.Type.ToString(), message.Id, message.Kind.ToString(), message.Message, null);
            }
        }
    }
}
