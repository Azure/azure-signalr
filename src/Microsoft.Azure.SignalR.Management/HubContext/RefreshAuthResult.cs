// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// The result of a successful <see cref="ServiceHubContext.RefreshAuthAsync"/> call.
    /// </summary>
    public sealed class RefreshAuthResult
    {
        internal RefreshAuthResult(string accessToken, int tokenLifetimeSeconds)
        {
            AccessToken = accessToken;
            TokenLifetimeSeconds = tokenLifetimeSeconds;
        }

        /// <summary>
        /// The refreshed service access token to return to the client. It is minted locally from the
        /// post-refresh claim set the service returns, for the connection's owning endpoint.
        /// </summary>
        public string AccessToken { get; }

        /// <summary>
        /// Seconds until the client should refresh again: <c>min(AccessTokenLifetime, expireTime - now)</c>.
        /// </summary>
        public int TokenLifetimeSeconds { get; }
    }
}
