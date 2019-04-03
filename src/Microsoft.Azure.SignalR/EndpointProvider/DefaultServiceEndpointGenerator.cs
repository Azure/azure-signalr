// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    internal sealed class DefaultServiceEndpointGenerator : IServiceEndpointGenerator
    {
        private const string ClientPath = "client";
        private const string ServerPath = "server";

        public string Endpoint { get; }

        public string AccessKey { get; }

        public string Version { get; }

        public int? Port { get; }

        public DefaultServiceEndpointGenerator(string endpoint, string accessKey, string version, int? port)
        {
            Endpoint = endpoint;
            AccessKey = accessKey;
            Version = version;
            Port = port;
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

            return $"{InternalGetEndpoint(ClientPath, hubName, applicationName)}{queryBuilder}";
        }

        public string GetServerAudience(string hubName, string applicationName) =>
            InternalGetAudience(ServerPath, hubName, applicationName);

        public string GetServerEndpoint(string hubName, string applicationName) =>
            InternalGetEndpoint(ServerPath, hubName, applicationName);

        private string InternalGetEndpoint(string path, string hubName, string applicationName)
        {
            var prefixedHubName = string.IsNullOrEmpty(applicationName) ? hubName.ToLower() : $"{applicationName.ToLower()}_{hubName.ToLower()}";
            return Port.HasValue ?
                $"{Endpoint}:{Port}/{path}/?hub={prefixedHubName}" :
                $"{Endpoint}/{path}/?hub={prefixedHubName}";
        }

        private string InternalGetAudience(string path, string hubName, string applicationName)
        {
            var prefixedHubName = string.IsNullOrEmpty(applicationName) ? hubName.ToLower() : $"{applicationName.ToLower()}_{hubName.ToLower()}";
            return $"{Endpoint}/{path}/?hub={prefixedHubName}";
        }
    }
}
