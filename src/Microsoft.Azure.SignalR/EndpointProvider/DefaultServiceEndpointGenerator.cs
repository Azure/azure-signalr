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
        public string Endpoint { get; }

        public string Version { get; }

        public int? Port { get; }

        public string ClientEndpoint { get; }

        public DefaultServiceEndpointGenerator(ServiceEndpoint endpoint)
        {
            Endpoint = endpoint.Endpoint;
            Version = endpoint.Version;
            Port = endpoint.Port;
            ClientEndpoint = endpoint.ClientEndpoint;
        }

        public string GetClientAudience(string hubName, string applicationName) =>
            InternalGetAudience(ClientPath, hubName, applicationName);


        public string GetClientEndpoint(string hubName, string applicationName, string originalPath, string queryString)
        {
            var queryBuilder = new StringBuilder();
            if (!string.IsNullOrEmpty(originalPath))
            {
                queryBuilder.Append("&")
                    .Append(Constants.QueryParameter.OriginalPath)
                    .Append("=")
                    .Append(WebUtility.UrlEncode(originalPath));
            }

            if (!string.IsNullOrEmpty(queryString))
            {
                queryBuilder.Append("&").Append(queryString);
            }

            return $"{InternalGetEndpoint(ClientPath, hubName, applicationName, ClientEndpoint ?? Endpoint)}{queryBuilder}";
        }

        public string GetServerAudience(string hubName, string applicationName) =>
            InternalGetAudience(ServerPath, hubName, applicationName);

        public string GetServerEndpoint(string hubName, string applicationName) =>
            InternalGetEndpoint(ServerPath, hubName, applicationName, Endpoint);

        private string GetPrefixedHubName(string applicationName, string hubName)
        {
            return string.IsNullOrEmpty(applicationName) ? hubName.ToLower() : $"{applicationName.ToLower()}_{hubName.ToLower()}";
        }

        private string InternalGetEndpoint(string path, string hubName, string applicationName, string target)
        {
            return Port.HasValue ?
                $"{target}:{Port}/{path}/?hub={GetPrefixedHubName(applicationName, hubName)}" :
                $"{target}/{path}/?hub={GetPrefixedHubName(applicationName, hubName)}";
        }

        private string InternalGetAudience(string path, string hubName, string applicationName)
        {
            return $"{Endpoint}/{path}/?hub={GetPrefixedHubName(applicationName, hubName)}";
        }
    }
}
