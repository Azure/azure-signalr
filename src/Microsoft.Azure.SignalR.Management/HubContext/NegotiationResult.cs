// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// The result of a <see cref="ServiceHubContext.NegotiateWithTokenLifetimeAsync"/> call.
    /// </summary>
    public sealed class NegotiationResult
    {
        internal NegotiationResult(string url, string accessToken, int tokenLifetimeSeconds)
        {
            Url = url;
            AccessToken = accessToken;
            TokenLifetimeSeconds = tokenLifetimeSeconds;
        }

        /// <summary>
        /// The URL for a client to connect to Azure SignalR Service.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// The access token for a client to connect to Azure SignalR Service.
        /// </summary>
        public string AccessToken { get; }

        /// <summary>
        /// Seconds until the client should refresh its authentication.
        /// </summary>
        public int TokenLifetimeSeconds { get; }
    }
}
