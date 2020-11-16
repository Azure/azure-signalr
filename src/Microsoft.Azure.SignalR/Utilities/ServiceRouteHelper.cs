// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceRouteHelper
    {
        public static async Task RedirectToService<THub>(HttpContext context, IList<IAuthorizeData> authorizationData) where THub : Hub
        {
            var handler = context.RequestServices.GetRequiredService<NegotiateHandler<THub>>();
            var loggerFactory = context.RequestServices.GetService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<ServiceRouteHelper>();

            if (authorizationData != null && !await AuthorizeHelper.AuthorizeAsync(context, authorizationData))
            {
                return;
            }

            NegotiationResponse negotiateResponse = null;
            try
            {
                negotiateResponse = await handler.Process(context);

                if (context.Response.HasStarted)
                {
                    // Inner handler already write to context.Response, no need to continue with error case
                    return;
                }

                // Consider it as internal server error when we don't successfully get negotiate response
                if (negotiateResponse == null)
                {
                    var message = "Unable to get the negotiate endpoint";
                    Log.NegotiateFailed(logger, message);
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync(message);
                    return;
                }
            }
            catch (AzureSignalRAccessTokenTooLongException ex)
            {
                Log.NegotiateFailed(logger, ex.Message);
                context.Response.StatusCode = 413;
                await context.Response.WriteAsync(ex.Message);
                return;
            }
            catch (AzureSignalRNotConnectedException e)
            {
                Log.NegotiateFailed(logger, e.Message);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync(e.Message);
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
