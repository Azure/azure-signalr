// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Web;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Owin;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ClientConnectionManager : IClientConnectionManager
    {
        private static readonly ClaimsPrincipal EmptyPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        private static readonly string[] SystemClaims =
        {
            "aud", // Audience claim, used by service to make sure token is matched with target resource.
            "exp", // Expiration time claims. A token is valid only before its expiration time.
            "iat", // Issued At claim. Added by default. It is not validated by service.
            "nbf"  // Not Before claim. Added by default. It is not validated by service.
        };

        private readonly HubConfiguration _configuration;

        public ClientConnectionManager(HubConfiguration configuration)
        {
            _configuration = configuration;
        }

        public AzureTransport CreateConnection(OpenConnectionMessage message, IServiceConnection serviceConnection)
        {
            var dispatcher = new HubDispatcher(_configuration);
            dispatcher.Initialize(_configuration.Resolver);

            var connectionId = message.ConnectionId;

            var responseStream = new MemoryStream();
            var hostContext = GetHostContext(message, responseStream, serviceConnection);

            if (dispatcher.Authorize(hostContext.Request))
            {
                // ProcessRequest checks if the connectionToken matches "{connectionid}:{userName}" format with context.User
                _ = dispatcher.ProcessRequest(hostContext);

                // TODO: check for errors written to the response
                if (hostContext.Response.StatusCode != 200)
                {
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

            var user = request.User = GetUserPrincipal(message);

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

        internal static ClaimsPrincipal GetUserPrincipal(OpenConnectionMessage message)
        {
            if (message.Claims == null || message.Claims.Length == 0)
            {
                return EmptyPrincipal;
            }

            var claims = new List<Claim>();
            var authenticationType = "Bearer";

            foreach (var claim in message.Claims)
            {
                // TODO: Add prefix "azure.signalr.user." to user claims instead of guessing them?
                if (claim.Type == Constants.ClaimType.AuthenticationType)
                {
                    authenticationType = claim.Value;
                }
                else if (!SystemClaims.Contains(claim.Type) && !claim.Type.StartsWith(Constants.ClaimType.AzureSignalRSysPrefix))
                {
                    claims.Add(claim);
                }
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
        }

        internal static string GetContentAndDispose(MemoryStream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
