// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// The result of a <see cref="ServiceHubContext.GetConnectionClaimsAsync"/> call.
    /// </summary>
    public sealed class ConnectionClaims
    {
        internal ConnectionClaims(IReadOnlyList<Claim> claims)
        {
            Claims = claims;
        }

        /// <summary>
        /// The connection's current claim set, usable as <c>PreviousUser</c> for an accept/reject policy.
        /// </summary>
        public IReadOnlyList<Claim> Claims { get; }
    }
}
