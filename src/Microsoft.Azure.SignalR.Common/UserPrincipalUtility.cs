// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal static class UserPrincipalUtility
    {
        private const string DefaultAuthenticationType = "Bearer";
        private static readonly string[] SystemClaims =
        {
            "aud", // Audience claim, used by service to make sure token is matched with target resource.
            "exp", // Expiration time claims. A token is valid only before its expiration time.
            "iat", // Issued At claim. Added by default. It is not validated by service.
            "nbf"  // Not Before claim. Added by default. It is not validated by service.
        };

        private static readonly ClaimsPrincipal EmptyPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        public static IEnumerable<Claim> GetSystemClaims(ClaimsPrincipal user)
        {
            if (user == null)
            {
                yield break;
            }

            var authenticationType = user.Identity?.AuthenticationType;
            if (authenticationType != null)
            {
                yield return new Claim(Constants.ClaimType.AuthenticationType, authenticationType);
            }

            var identity = user.Identity as ClaimsIdentity;

            if (identity == null)
            {
                yield break;
            }

            var nameType = identity.NameClaimType;
            if (nameType != null)
            {
               yield return new Claim(Constants.ClaimType.NameType, nameType);
            }

            var roleType = identity.RoleClaimType;
            if (roleType != null)
            {
                yield return new Claim(Constants.ClaimType.RoleType, roleType);
            }
        }

        public static ClaimsPrincipal GetUserPrincipal(this OpenConnectionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (message.Claims == null || message.Claims.Length == 0)
            {
                return EmptyPrincipal;
            }

            var claims = new List<Claim>();
            var authenticationType = DefaultAuthenticationType;
            string nameType = null;
            string roleType = null;
            foreach (var claim in message.Claims)
            {
                if (claim.Type == Constants.ClaimType.AuthenticationType)
                {
                    authenticationType = claim.Value;
                }
                else if (claim.Type == Constants.ClaimType.NameType)
                {
                    nameType = claim.Value;
                }
                else if (claim.Type == Constants.ClaimType.RoleType)
                {
                    roleType = claim.Value;
                }
                else if (!SystemClaims.Contains(claim.Type) && !claim.Type.StartsWith(Constants.ClaimType.AzureSignalRSysPrefix))
                {
                    claims.Add(claim);
                }
            }

            if (claims.Count == 0)
            {
                return EmptyPrincipal;
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType, nameType, roleType));
        }
    }
}
