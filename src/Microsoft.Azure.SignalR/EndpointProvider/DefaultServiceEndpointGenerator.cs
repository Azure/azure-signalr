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

        public string HubPrefix { get; }

        public int? Port { get; }

        public DefaultServiceEndpointGenerator(string endpoint, string accessKey, string version, int? port, string hubPrefix)
        {
            Endpoint = endpoint;
            AccessKey = accessKey;
            Version = version;
            Port = port;
            HubPrefix = hubPrefix;
        }

        public string GetClientAudience(string hubName) =>
            InternalGetAudience(ClientPath, hubName, HubPrefix);


        public string GetClientEndpoint(string hubName, string originalPath, string queryString)
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

            return $"{InternalGetEndpoint(ClientPath, hubName, HubPrefix)}{queryBuilder}";
        }

        public string GetServerAudience(string hubName) =>
            InternalGetAudience(ServerPath, hubName, HubPrefix);

        public string GetServerEndpoint(string hubName) =>
            InternalGetEndpoint(ServerPath, hubName, HubPrefix);

        private string InternalGetEndpoint(string path, string hubName, string hubPrefix)
        {
            var prefixedHubName = string.IsNullOrEmpty(hubPrefix) ? hubName.ToLower() : $"{hubPrefix.ToLower()}_{hubName.ToLower()}";
            return Port.HasValue ?
                $"{Endpoint}:{Port}/{path}/?hub={prefixedHubName}" :
                $"{Endpoint}/{path}/?hub={prefixedHubName}";
        }

        private string InternalGetAudience(string path, string hubName, string hubPrefix)
        {
            var prefixedHubName = string.IsNullOrEmpty(hubPrefix) ? hubName.ToLower() : $"{hubPrefix.ToLower()}_{hubName.ToLower()}";
            return $"{Endpoint}/{path}/?hub={prefixedHubName}";
        }
    }
}
