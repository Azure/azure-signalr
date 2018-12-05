// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Web;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Owin;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ClientConnectionManager : IClientConnectionManager
    {
        private readonly HubConfiguration _configuration;
        private readonly ILogger _logger;

        public ClientConnectionManager(HubConfiguration configuration)
        {
            _configuration = configuration;
            var loggerFactory = configuration.Resolver.Resolve<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            _logger = loggerFactory.CreateLogger<ClientConnectionManager>();
        }

        public IServiceTransport CreateConnection(OpenConnectionMessage message, IServiceConnection serviceConnection)
        {
            var dispatcher = new HubDispatcher(_configuration);
            dispatcher.Initialize(_configuration.Resolver);

            var responseStream = new MemoryStream();
            var hostContext = GetHostContext(message, responseStream, serviceConnection);

            if (dispatcher.Authorize(hostContext.Request))
            {
                // ProcessRequest checks if the connectionToken matches "{connectionid}:{userName}" format with context.User
                _ = dispatcher.ProcessRequest(hostContext);

                // TODO: check for errors written to the response
                if (hostContext.Response.StatusCode != 200)
                {
                    Log.ProcessRequestError(_logger, message.ConnectionId, hostContext.Request.QueryString.ToString());
                    Debug.Fail("Response StatusCode is " + hostContext.Response.StatusCode);
                    var errorResponse = GetContentAndDispose(responseStream);
                    throw new InvalidOperationException(errorResponse);
                }

                return (AzureTransport)hostContext.Environment[AspNetConstants.Context.AzureSignalRTransportKey];
            }

            // This happens when hub is not found
            Debug.Fail("Unauthorized");
            throw new InvalidOperationException("Unable to authorize request");
        }

        internal HostContext GetHostContext(OpenConnectionMessage message, Stream responseStream, IServiceConnection serviceConnection)
        {
            var connectionId = message.ConnectionId;
            var context = new OwinContext();
            var response = context.Response;
            var request = context.Request;

            response.Body = responseStream;

            var user = request.User = message.GetUserPrincipal();

            request.Path = new PathString("/");

            var userToken = string.IsNullOrEmpty(user.Identity.Name) ? string.Empty : ":" + user.Identity.Name;

            // TODO: when https://github.com/SignalR/SignalR/issues/4175 is resolved, we can get rid of paring query string
            var queryCollection = HttpUtility.ParseQueryString(message.QueryString ?? string.Empty);
            queryCollection[AspNetConstants.QueryString.ConnectionToken] = $"{connectionId}{userToken}";

            request.QueryString = new QueryString(queryCollection.ToString());

            if (message.Headers != null)
            {
                foreach (var pair in message.Headers)
                {
                    request.Headers.Add(pair.Key, pair.Value);
                }
            }

            context.Environment[AspNetConstants.Context.AzureServiceConnectionKey] = serviceConnection;
            return new HostContext(context.Environment);
        }

        internal static string GetContentAndDispose(MemoryStream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, string, Exception> _processRequestError =
                LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(1, "ProcessRequestError"), "ProcessRequest for {connectionId} fails with {queryString} ");

            public static void ProcessRequestError(ILogger logger, string connectionId, string queryString)
            {
                _processRequestError(logger, connectionId, queryString, null);
            }
        }
    }
}
