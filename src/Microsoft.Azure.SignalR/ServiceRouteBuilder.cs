// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// Maps incoming requests to <see cref="Hub"/> types.
    /// </summary>
    public class ServiceRouteBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private readonly RouteBuilder _routes;
        private readonly NegotiateHandler _negotiateHandler;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRouteBuilder"/> class.
        /// </summary>
        /// <param name="routes">The routes builder.</param>
        public ServiceRouteBuilder(RouteBuilder routes)
        {
            _routes = routes;
            _serviceProvider = _routes.ServiceProvider;
            _negotiateHandler = _serviceProvider.GetRequiredService<NegotiateHandler>();

            var loggerFactory = _serviceProvider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            _logger = loggerFactory.CreateLogger<ServiceRouteBuilder>(); 
        }

        /// <summary>
        /// Maps incoming requests with the specified path to the specified <see cref="Hub"/> type.
        /// </summary>
        /// <typeparam name="THub">The <see cref="Hub"/> type to map requests to.</typeparam>
        /// <param name="path">The request path.</param>
        public void MapHub<THub>(string path) where THub : Hub
            => MapHub<THub>(new PathString(path));

        /// <summary>
        /// Maps incoming requests with the specified path to the specified <see cref="Hub"/> type.
        /// </summary>
        /// <typeparam name="THub">The <see cref="Hub"/> type to map requests to.</typeparam>
        /// <param name="path">The request path.</param>
        public void MapHub<THub>(PathString path) where THub: Hub
        {
            // find auth attributes
            var authorizeAttributes = typeof(THub).GetCustomAttributes<AuthorizeAttribute>(inherit: true);
            var authorizationData = new List<IAuthorizeData>();
            foreach (var attribute in authorizeAttributes)
            {
                authorizationData.Add(attribute);
            }
            _routes.MapRoute(path + Constants.Path.Negotiate, c => RedirectToService(c, typeof(THub).Name, authorizationData));

            Start<THub>();
        }

        private async Task RedirectToService(HttpContext context, string hubName, IList<IAuthorizeData> authorizationData)
        {
            if (!await AuthorizeHelper.AuthorizeAsync(context, authorizationData))
            {
                return;
            }

            NegotiationResponse negotiateResponse = null;
            try
            {
                negotiateResponse = _negotiateHandler.Process(context, hubName);

                if (context.Response.StatusCode >= 400)
                {
                    // Inner handler already write to context.Response, no need to continue with error case
                    return;
                }

                // Consider it as internal server error when we don't successfully get negotiate response
                if (negotiateResponse == null)
                {
                    if (!context.Response.HasStarted)
                    {
                        Log.NegotiateFailed(_logger, "Unable to get the negotiate endpoint");
                        context.Response.StatusCode = 500;
                    }

                    return;
                }
            }
            catch (AzureSignalRAccessTokenTooLongException ex)
            {
                Log.NegotiateFailed(_logger, ex.Message);
                context.Response.StatusCode = 413;
                await HttpResponseWritingExtensions.WriteAsync(context.Response, ex.Message);
                return;
            }
            catch (AzureSignalRNotConnectedException e)
            {
                Log.NegotiateFailed(_logger, e.Message);
                context.Response.StatusCode = 500;
                await HttpResponseWritingExtensions.WriteAsync(context.Response, e.Message);
                return;
            }

            var writer = new MemoryBufferWriter();
            try
            {
                context.Response.ContentType = "application/json";
                NegotiateProtocol.WriteResponse(negotiateResponse, writer);
                // Write it out to the response with the right content length
                context.Response.ContentLength = writer.Length;
                await writer.CopyToAsync(context.Response.Body);
            }
            finally
            {
                writer.Reset();
            }
        }

        private void Start<THub>() where THub : Hub
        {
            var app = new ConnectionBuilder(_serviceProvider)
                .UseHub<THub>()
                .Build();

            var dispatcher = _serviceProvider.GetRequiredService<ServiceHubDispatcher<THub>>();
            dispatcher.Start(app);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _negotiateFailed =
                LoggerMessage.Define<string>(LogLevel.Critical, new EventId(1, "NegotiateFailed"), "Client negotiate failed: {Error}");

            public static void NegotiateFailed(ILogger logger, string error)
            {
                _negotiateFailed(logger, error, null);
            }
        }
    }
}
