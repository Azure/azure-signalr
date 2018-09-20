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

        public string GetClientAudience(string hubName) =>
            InternalGetAudience(ClientPath, hubName);

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

            return $"{InternalGetEndpoint(ClientPath, hubName)}{queryBuilder}";
        }

        public string GetServerAudience(string hubName) =>
            InternalGetAudience(ServerPath, hubName);

        public string GetServerEndpoint(string hubName) =>
            InternalGetEndpoint(ServerPath, hubName);

        private string InternalGetEndpoint(string path, string hubName) =>
            Port.HasValue ?
            $"{Endpoint}:{Port}/{path}/?hub={hubName.ToLower()}" :
            $"{Endpoint}/{path}/?hub={hubName.ToLower()}";

        private string InternalGetAudience(string path, string hubName) =>
            $"{Endpoint}/{path}/?hub={hubName.ToLower()}";
    }
}
