// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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
    internal class ServiceHubDispatcher : HubDispatcher
    {
        private static readonly ProtocolResolver ProtocolResolver = new ProtocolResolver();

        private readonly string _appName;
        private readonly Func<IOwinContext, IEnumerable<Claim>> _claimsProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private IServiceEndpointProvider _endpoint;

        public ServiceHubDispatcher(HubConfiguration configuration, string appName, ServiceOptions options, ILoggerFactory loggerFactory) : base(configuration)
        {
            _appName = appName ?? throw new ArgumentException(nameof(appName));
            _claimsProvider = options?.ClaimsProvider;
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<ServiceHubDispatcher>();
        }

        public override void Initialize(IDependencyResolver resolver)
        {
            _endpoint = resolver.Resolve<IServiceEndpointProvider>();
            base.Initialize(resolver);
        }

        public override Task ProcessRequest(HostContext context)
        {
            // Redirect negotiation to service
            if (IsNegotiationRequest(context.Request))
            {
                return ProcessNegotiationRequest(context);
            }

            return base.ProcessRequest(context);
        }

        private Task ProcessNegotiationRequest(HostContext context)
        {
            string accessToken = null;
            var claims = BuildClaims(context);

            // Redirect to Service
            var url = _endpoint.GetClientEndpoint();
            try
            {
                accessToken = _endpoint.GenerateClientAccessToken(claims);
            }
            catch (AccessTokenTooLongException ex)
            {
                Log.NegotiateFailed(_logger, ex.Message);
                context.Response.StatusCode = 413;
                return context.Response.End(ex.Message);
            }

            return SendJsonResponse(context, GetRedirectNegotiateResponse(url, accessToken));
        }

        private IEnumerable<Claim> BuildClaims(HostContext context)
        {
            // Pass appname through jwt token to client, so that when client establishes connection with service, it will also create a corresponding AppName-connection
            yield return new Claim(Constants.ClaimType.AppName, _appName);
            var owinContext = new OwinContext(context.Environment);
            var user = owinContext.Authentication?.User;
            var userId = UserIdProvider?.GetUserId(context.Request);

            var claims = ClaimsUtility.BuildJwtClaims(user, userId, GetClaimsProvider(owinContext));

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
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "NegotiateFailed"), "Client Negotiate Failed: {Error}");

            public static void NegotiateFailed(ILogger logger, string error)
            {
                _negotiateFailed(logger, error, null);
            }
        }
    }
}
