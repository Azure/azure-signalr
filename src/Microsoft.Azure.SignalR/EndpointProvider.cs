// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class EndpointProvider
    {
        private const int ClientPort = 5001;
        private const int ServerPort = 5002;

        private readonly string _endpoint;

        public EndpointProvider(string endpoint)
        {
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentNullException(nameof(endpoint));
            }
            _endpoint = endpoint.TrimEnd('/');
        }

        public string GetClientEndpoint<THub>() where THub : Hub
        {
            return GetClientEndpoint(typeof(THub).Name);
        }

        public string GetClientEndpoint(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return InternalGetEndpoint(ClientPort, "client", hubName);
        }

        public string GetServerEndpoint<THub>() where THub : Hub
        {
            return GetServerEndpoint(typeof(THub).Name);
        }

        public string GetServerEndpoint(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return InternalGetEndpoint(ServerPort, "server", hubName);
        }

        private string InternalGetEndpoint(int port, string path, string hubName)
        {
            return $"{_endpoint}:{port}/{path}/?hub={hubName.ToLower()}";
        }
    }
}
