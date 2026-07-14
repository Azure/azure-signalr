// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// The result of a successful <see cref="ServiceHubContext.RefreshConnectionAuthenticationAsync"/> call.
    /// </summary>
    public sealed class RefreshConnectionAuthenticationResult
    {
        internal RefreshConnectionAuthenticationResult(string accessToken, int tokenLifetimeSeconds)
        {
            AccessToken = accessToken;
            TokenLifetimeSeconds = tokenLifetimeSeconds;
        }

        /// <summary>
        /// The refreshed service access token to return to the client.
        /// </summary>
        public string AccessToken { get; }

        /// <summary>
        /// Seconds until the client should refresh again.
        /// </summary>
        public int TokenLifetimeSeconds { get; }
    }
}
