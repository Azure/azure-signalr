// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class NegotiateMiddleware : OwinMiddleware
    {
        private static readonly ProtocolResolver ProtocolResolver = new ProtocolResolver();

        private readonly string _appName;
        private readonly Func<IOwinContext, IEnumerable<Claim>> _claimsProvider;
        private readonly ILogger _logger;

        private readonly IServiceEndpointManager _endpointManager;
        private readonly IEndpointRouter _router;
        private readonly IUserIdProvider _provider;
        private readonly HubConfiguration _configuration;

        private readonly string _serverName;
        private readonly ServerStickyMode _mode;
        private readonly bool _isolateApp;

        public NegotiateMiddleware(OwinMiddleware next, HubConfiguration configuration, string appName, IServiceEndpointManager endpointManager, IEndpointRouter router, ServiceOptions options, IServerNameProvider serverNameProvider, ILoggerFactory loggerFactory)
            : base(next)
        {
            _configuration = configuration;
            _provider = configuration.Resolver.Resolve<IUserIdProvider>();
            _appName = appName ?? throw new ArgumentNullException(nameof(appName));
            _claimsProvider = options?.ClaimsProvider;
            _endpointManager = endpointManager ?? throw new ArgumentNullException(nameof(endpointManager));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _logger = loggerFactory?.CreateLogger<NegotiateMiddleware>() ?? throw new ArgumentNullException(nameof(loggerFactory));
            _serverName = serverNameProvider?.GetName();
            _mode = options.ServerStickyMode;
            _isolateApp = options.IsolateApplication;
        }

        public override Task Invoke(IOwinContext owinContext)
        {
            if (owinContext == null)
            {
                throw new ArgumentNullException(nameof(owinContext));
            }

            var context = new HostContext(owinContext.Environment);

            if (IsNegotiationRequest(context.Request))
            {
                return ProcessNegotiationRequest(owinContext, context);
            }

            return Next.Invoke(owinContext);
        }

        private Task ProcessNegotiationRequest(IOwinContext owinContext, HostContext context)
        {
            string accessToken = null;
            var claims = BuildClaims(owinContext, context.Request);
            
            var dispatcher = new HubDispatcher(_configuration);
            try
            {
                dispatcher.Initialize(_configuration.Resolver);
                if (!dispatcher.Authorize(context.Request))
                {
                    string error = null;
                    if (context.Request.User != null && context.Request.User.Identity.IsAuthenticated)
                    {
                        // If the user is authenticated and authorize failed then 403
                        error = "Forbidden";
                        context.Response.StatusCode = 403;
                    }
                    else
                    {
                        // If failed to authorize the request then return 401
                        error = "Unauthorized";
                        context.Response.StatusCode = 401;
                    }
                    Log.NegotiateFailed(_logger, error);
                    return context.Response.End(error);
                }
            }
            catch (Exception e)
            {
                Log.NegotiateFailed(_logger, e.Message);
                context.Response.StatusCode = 500;
                return context.Response.End("");
            }

            IServiceEndpointProvider provider;
            try
            {
                provider = _endpointManager.GetEndpointProvider(_router.GetNegotiateEndpoint(owinContext, _endpointManager.Endpoints));

                // When status code changes, we consider the inner router changed the repsonse, then we stop here
                if (context.Response.StatusCode != 200)
                {
                    // Inner handler already write to context.Response, no need to continue with error case
                    return Task.CompletedTask;
                }

                // Consider it as internal server error when we don't successfully get negotiate response
                if (provider == null)
                {
                    var message = "Unable to get the negotiate endpoint";
                    Log.NegotiateFailed(_logger, message);
                    context.Response.StatusCode = 500;
                    return context.Response.End(message);
                }
            }
            catch (AzureSignalRNotConnectedException e)
            {
                Log.NegotiateFailed(_logger, e.Message);
                context.Response.StatusCode = 500;
                return context.Response.End(e.Message);
            }

            // Redirect to Service
            // TODO: add OriginalPath and QueryString when the clients support it
            var url = provider.GetClientEndpoint(null, null, null);
            try
            {
                accessToken = provider.GenerateClientAccessToken(null, claims);
            }
            catch (AzureSignalRAccessTokenTooLongException ex)
            {
                Log.NegotiateFailed(_logger, ex.Message);
                context.Response.StatusCode = 413;
                return context.Response.End(ex.Message);
            }

            return SendJsonResponse(context, GetRedirectNegotiateResponse(url, accessToken));
        }

        private IEnumerable<Claim> BuildClaims(IOwinContext owinContext, IRequest request)
        {
            // Pass appname through jwt token to client, so that when client establishes connection with service, it will also create a corresponding AppName-connection
            yield return new Claim(Constants.ClaimType.AppName, _appName);
            var user = owinContext.Authentication?.User;
            var userId = _provider?.GetUserId(request);

            var claims = ClaimsUtility.BuildJwtClaims(user, userId, GetClaimsProvider(owinContext), _serverName, _mode);

            if (_isolateApp)
            {
                yield return new Claim(Constants.ClaimType.IsolateApplication, "");
            }

            foreach (var claim in claims)
            {
                yield return claim;
            }
        }

        private Func<IEnumerable<Claim>> GetClaimsProvider(IOwinContext context)
        {
            if (_claimsProvider == null)
            {
                return null;
            }

            return () => _claimsProvider.Invoke(context);
        }

        private static string GetRedirectNegotiateResponse(string url, string token)
        {
            var sb = new StringBuilder();
            using (var jsonWriter = new JsonTextWriter(new StringWriter(sb)))
            {
                jsonWriter.WriteStartObject();
                jsonWriter.WritePropertyName("ProtocolVersion");
                jsonWriter.WriteValue("2.0");
                jsonWriter.WritePropertyName("RedirectUrl");
                jsonWriter.WriteValue(url);
                jsonWriter.WritePropertyName("AccessToken");
                jsonWriter.WriteValue(token);
                jsonWriter.WriteEndObject();
            }

            return sb.ToString();
        }

        private static bool IsNegotiationRequest(IRequest request)
        {
            return request.LocalPath.EndsWith(Constants.Path.Negotiate, StringComparison.OrdinalIgnoreCase);
        }

        private static Task SendJsonResponse(HostContext context, string jsonPayload)
        {
            var callback = context.Request.QueryString["callback"];
            if (String.IsNullOrEmpty(callback))
            {
                // Send normal JSON response
                context.Response.ContentType = JsonUtility.JsonMimeType;
                return context.Response.End(jsonPayload);
            }

            // JSONP response is no longer supported.
            context.Response.StatusCode = 400;
            return context.Response.End("JSONP is no longer supported.");
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _negotiateFailed =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "NegotiateFailed"), "Client negotiate failed: {Error}");

            public static void NegotiateFailed(ILogger logger, string error)
            {
                _negotiateFailed(logger, error, null);
            }
        }
    }
}
