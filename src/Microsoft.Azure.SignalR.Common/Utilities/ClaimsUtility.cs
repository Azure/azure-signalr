// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal static class ClaimsUtility
    {
        private const string DefaultAuthenticationType = "Bearer";
        private static readonly string[] SystemClaims =
        {
            "aud", // Audience claim, used by service to make sure token is matched with target resource.
            "exp", // Expiration time claims. A token is valid only before its expiration time.
            "iat", // Issued At claim. Added by default. It is not validated by service.
            "nbf"  // Not Before claim. Added by default. It is not validated by service.
        };

        private static readonly ClaimsIdentity DefaultClaimsIdentity = new ClaimsIdentity();
        private static readonly ClaimsPrincipal EmptyPrincipal = new ClaimsPrincipal(DefaultClaimsIdentity);
        private static readonly string DefaultNameClaimType = DefaultClaimsIdentity.NameClaimType;
        private static readonly string DefaultRoleClaimType = DefaultClaimsIdentity.RoleClaimType;

        public static IEnumerable<Claim> BuildJwtClaims(ClaimsPrincipal user, string userId, Func<IEnumerable<Claim>> claimsProvider, string serverName = null, ServerStickyMode mode = ServerStickyMode.Disabled)
        {
            if (userId != null)
            {
                yield return new Claim(Constants.ClaimType.UserId, userId);
            }

            if (serverName != null && mode != ServerStickyMode.Disabled)
            {
                yield return new Claim(Constants.ClaimType.ServerName, serverName);
                yield return new Claim(Constants.ClaimType.ServerStickyMode, mode.ToString());
            }

            var authenticationType = user?.Identity?.AuthenticationType;

            // No need to pass it when the authentication type is Bearer
            if (authenticationType != null && authenticationType != DefaultAuthenticationType)
            {
                yield return new Claim(Constants.ClaimType.AuthenticationType, authenticationType);
            }

            // Return custom NameClaimType and RoleClaimType
            // We can have multiple Identities, for now, choose the default one 
            if (user?.Identity is ClaimsIdentity identity)
            {
                var nameType = identity.NameClaimType;
                if (nameType != null && nameType != DefaultNameClaimType)
                {
                    yield return new Claim(Constants.ClaimType.NameType, nameType);
                }

                var roleType = identity.RoleClaimType;
                if (roleType != null && roleType != DefaultRoleClaimType)
                {
                    yield return new Claim(Constants.ClaimType.RoleType, roleType);
                }
            }

            // return customer's claims
            var customerClaims = claimsProvider == null ? user?.Claims : claimsProvider.Invoke();
            if (customerClaims != null)
            {
                foreach (var claim in customerClaims)
                {
                    // Add AzureSignalRUserPrefix if customer's claim name is duplicated with SignalR system claims.
                    // And split it when return from SignalR Service.
                    if (SystemClaims.Contains(claim.Type))
                    {
                        yield return new Claim(Constants.ClaimType.AzureSignalRUserPrefix + claim.Type, claim.Value);
                    }
                    else
                    {
                        yield return claim;
                    }
                }
            }
        }

        public static ClaimsPrincipal GetUserPrincipal(this OpenConnectionMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return GetUserPrincipal(message.Claims);
        }

        internal static ClaimsPrincipal GetUserPrincipal(Claim[] messageClaims)
        {
            if (messageClaims == null || messageClaims.Length == 0)
            {
                return EmptyPrincipal;
            }

            var claims = new List<Claim>();
            var authenticationType = DefaultAuthenticationType;
            string nameType = null;
            string roleType = null;
            foreach (var claim in messageClaims)
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
                else if (claim.Type.StartsWith(Constants.ClaimType.AzureSignalRUserPrefix))
                {
                    var claimName = claim.Type.Substring(Constants.ClaimType.AzureSignalRUserPrefix.Length);
                    claims.Add(new Claim(claimName, claim.Value));
                }
                else if (!SystemClaims.Contains(claim.Type) && !claim.Type.StartsWith(Constants.ClaimType.AzureSignalRSysPrefix))
                {
                    claims.Add(claim);
                }
            }

            if (claims.Count == 0)
            {
                // For JWT token, the authenticated claims must contain non-system claims
                return EmptyPrincipal;
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType, nameType, roleType));
        }
    }
}
