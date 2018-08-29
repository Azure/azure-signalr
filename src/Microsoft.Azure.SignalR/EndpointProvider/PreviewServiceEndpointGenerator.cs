// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;

namespace Microsoft.Azure.SignalR
{
    internal sealed class PreviewServiceEndpointGenerator : IServiceEndpointGenerator
    {
        private const int ClientPort = 5001;
        private const int ServerPort = 5002;
        private const string ClientPath = "client";
        private const string ServerPath = "server";

        public string Endpoint { get; }

        public string AccessKey { get; }

        public PreviewServiceEndpointGenerator(string endpoint, string accessKey)
        {
            Endpoint = endpoint;
            AccessKey = accessKey;
        }

        public string GetClientAudience(string hubName) =>
            InternalGetEndpoint(ClientPort, ClientPath, hubName);

        public string GetClientEndpoint(string hubName, string originalPath) =>
            string.IsNullOrEmpty(originalPath)
                ? InternalGetEndpoint(ClientPort, ClientPath, hubName)
                : GetClientEndpoint(ClientPort, ClientPath, hubName, originalPath);

        public string GetServerAudience(string hubName) =>
            InternalGetEndpoint(ServerPort, ServerPath, hubName);

        public string GetServerEndpoint(string hubName) =>
            InternalGetEndpoint(ServerPort, ServerPath, hubName);

        private string GetClientEndpoint(int port, string path, string hubName, string originalPath) =>
            $"{InternalGetEndpoint(port, path, hubName)}&{Constants.QueryParameter.OriginalPath}={WebUtility.UrlEncode(originalPath)}";

        private string InternalGetEndpoint(int port, string path, string hubName) =>
            $"{Endpoint}:{port}/{path}/?hub={hubName.ToLower()}";
    }
}
