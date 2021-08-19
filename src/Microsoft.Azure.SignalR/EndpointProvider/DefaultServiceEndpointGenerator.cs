// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    internal sealed class DefaultServiceEndpointGenerator : IServiceEndpointGenerator
    {
        private const string ClientPath = "client";
        private const string ServerPath = "server";

        public string Version { get; }

        public string Endpoint { get; }
        public string ServerEndpoint { get; }
        public string ClientEndpoint { get; }

        public DefaultServiceEndpointGenerator(ServiceEndpoint endpoint)
        {
            Version = endpoint.Version;
            Endpoint = endpoint.Endpoint;
            ServerEndpoint = endpoint.ServerEndpoint;
            ClientEndpoint = endpoint.ClientEndpoint;
        }

        public string GetClientAudience(string hubName, string applicationName) =>
            InternalGetAudience(ClientPath, hubName, applicationName);


        public string GetClientEndpoint(string hubName, string applicationName, string originalPath, string queryString)
        {
            var uriBuilder = new UriBuilder(ClientEndpoint)
            {
                Path = $"{ClientPath}/"
            };

            var hub = GetPrefixedHubName(applicationName, hubName);
            var queryBuilder = new StringBuilder("hub=").Append(hub);

            if (!string.IsNullOrEmpty(originalPath))
            {
                queryBuilder.Append("&")
                    .Append(Constants.QueryParameter.OriginalPath)
                    .Append("=")
                    .Append(WebUtility.UrlEncode(originalPath));
            }

            if (!string.IsNullOrEmpty(queryString))
            {
                queryBuilder.Append("&").Append(queryString.TrimStart('?'));
            }

            uriBuilder.Query = queryBuilder.ToString();
            return uriBuilder.Uri.AbsoluteUri;
        }

        public string GetServerAudience(string hubName, string applicationName) =>
            InternalGetAudience(ServerPath, hubName, applicationName);

        public string GetServerEndpoint(string hubName, string applicationName)
        {
            var uriBuilder = new UriBuilder(ServerEndpoint)
            {
                Path = $"{ServerPath}/",
                Query = $"hub={GetPrefixedHubName(applicationName, hubName)}",
            };
            return uriBuilder.Uri.AbsoluteUri;
        }

        private string GetPrefixedHubName(string applicationName, string hubName)
        {
            return string.IsNullOrEmpty(applicationName) ? hubName.ToLower() : $"{applicationName.ToLower()}_{hubName.ToLower()}";
        }

        private string InternalGetAudience(string path, string hubName, string applicationName)
        {
            return $"{Endpoint}/{path}/?hub={GetPrefixedHubName(applicationName, hubName)}";
        }
    }
}
