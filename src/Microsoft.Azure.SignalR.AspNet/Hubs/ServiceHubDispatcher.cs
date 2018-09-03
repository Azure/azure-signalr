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
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceHubDispatcher : HubDispatcher
    {
        private static readonly ProtocolResolver ProtocolResolver = new ProtocolResolver();

        private IServiceEndpointProvider _endpoint;

        public ServiceHubDispatcher(HubConfiguration configuration) : base(configuration)
        {
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
            var user = new Owin.OwinContext(context.Environment).Authentication.User;
            var claims = user?.Claims;
            var authenticationType = user?.Identity?.AuthenticationType;
            var userId = UserIdProvider.GetUserId(context.Request);
            var advancedClaims = claims?.ToList() ?? new List<Claim>();

            if (authenticationType != null)
            {
                advancedClaims.Add(new Claim(Constants.ClaimType.AuthenticationType, authenticationType));
            }

            advancedClaims.Add(new Claim(Constants.ClaimType.UserId, userId));

            // Redirect to Service
            var url = _endpoint.GetClientEndpoint();
            var accessToken = _endpoint.GenerateClientAccessToken(advancedClaims);

            return SendJsonResponse(context, GetRedirectNegotiateResponse(url, accessToken));
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
    }
}
