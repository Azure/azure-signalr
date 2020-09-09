// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Claims;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;

namespace Microsoft.Azure.SignalR.Common
{
    internal static class HttpContextExtenstions
    {
        internal static string GetUserIdentifier(this ConnectionContext connectionContext)
        {
            var user = connectionContext.Features.Get<IConnectionUserFeature>()?.User;
            if (user != null)
            {
                var customUserIdClaim = user.FindFirst(Constants.ClaimTypes.UserIdClaimType);
                return customUserIdClaim != null
                    ? customUserIdClaim.Value
                    : user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }

            return null;
        }
    }
}
