// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;

using Microsoft.AspNetCore.Http.Connections;
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
            "nbf",  // Not Before claim. Added by default. It is not validated by service.
            "iss",  // "iss" is a system claim. It is not validated by service.
            "actort",
            "acr",
            "azp",
            "c_hash",
            "jti",
            "nonce",
        };
        private static readonly HashSet<string> TokenGeneratedClaimTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            "aud",
            "exp",
            "iat",
            "iss",
            "nbf",
        };

        private static readonly ClaimsIdentity DefaultClaimsIdentity = new ClaimsIdentity();
        private static readonly ClaimsPrincipal EmptyPrincipal = new ClaimsPrincipal(DefaultClaimsIdentity);
        private static readonly string DefaultNameClaimType = DefaultClaimsIdentity.NameClaimType;
        private static readonly string DefaultRoleClaimType = DefaultClaimsIdentity.RoleClaimType;

        public static IEnumerable<Claim> BuildJwtClaims(
            ClaimsPrincipal user,
            string userId,
            Func<IEnumerable<Claim>> claimsProvider,
            string serverName = null,
            ServerStickyMode mode = ServerStickyMode.Disabled,
            bool enableDetailedErrors = false,
            int endpointsCount = 1,
            int? maxPollInterval = null,
            bool isDiagnosticClient = false, int handshakeTimeout = Constants.Periods.DefaultHandshakeTimeout,
            HttpTransportType? httpTransportType = null,
            bool closeOnAuthenticationExpiration = false, DateTimeOffset? authenticationExpiresOn = default
            )
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

            if (isDiagnosticClient)
            {
                yield return new Claim(Constants.ClaimType.DiagnosticClient, "true");
            }

            if (handshakeTimeout != Constants.Periods.DefaultHandshakeTimeout)
            {
                yield return new Claim(Constants.ClaimType.CustomHandshakeTimeout, handshakeTimeout.ToString(CultureInfo.InvariantCulture));
            }

            var authenticationType = user?.Identity?.AuthenticationType;

            // No need to pass it when the authentication type is Bearer
            if (authenticationType != null && authenticationType != DefaultAuthenticationType)
            {
                yield return new Claim(Constants.ClaimType.AuthenticationType, authenticationType);
            }

            // Trace multiple instances
            if (endpointsCount > 1)
            {
                yield return new Claim(Constants.ClaimType.ServiceEndpointsCount, endpointsCount.ToString(CultureInfo.InvariantCulture));
            }

            if (enableDetailedErrors)
            {
                yield return new Claim(Constants.ClaimType.EnableDetailedErrors, true.ToString());
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

            // add claim if exists, validation is in DI
            if (maxPollInterval.HasValue)
            {
                yield return new Claim(Constants.ClaimType.MaxPollInterval, maxPollInterval.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (httpTransportType.HasValue)
            {
                yield return new Claim(Constants.ClaimType.HttpTransportType, ((int)httpTransportType).ToString(CultureInfo.InvariantCulture));
            }

            if (closeOnAuthenticationExpiration
                && authenticationExpiresOn.HasValue
                && authenticationExpiresOn != DateTimeOffset.MaxValue)
            {
                yield return new Claim(Constants.ClaimType.CloseOnAuthExpiration, "true");
                yield return new Claim(Constants.ClaimType.AuthExpiresOn, authenticationExpiresOn.Value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
            }

            // return customer's claims
            var customerClaims = (claimsProvider == null ? user?.Claims : claimsProvider.Invoke())?.ToArray();
            if (customerClaims != null)
            {
                // According to the spec https://datatracker.ietf.org/doc/html/rfc7519#section-4.1
                // the "sub" value is a case-sensitive string containing a StringOrURI value
                // "sub" is used as the UserId if userId is not specified
                // If "sub" exists, we here make sure only one "sub" is preserved, others will be renamed as user claim type
                var hasSubClaim = false;
                foreach (var claim in customerClaims)
                {
                    if (claim.Type == "sub")
                    {
                        if (hasSubClaim)
                        {
                            // only the first "sub" is preserved as "sub", others will be renamed as user claims
                            yield return new Claim(Constants.ClaimType.AzureSignalRUserPrefix + claim.Type, claim.Value);
                        }
                        else
                        {
                            hasSubClaim = true;
                            // The first "sub" would be also considered as "nameIdentifier" and used as SignalR's UserIdentifier
                            yield return claim;
                        }

                    }
                    // Add AzureSignalRUserPrefix if customer's claim name is duplicated with SignalR system claims.
                    else if (SystemClaims.Contains(claim.Type))
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

        internal static IReadOnlyList<Claim> PrepareClaimsForToken(IReadOnlyList<Claim> claims)
        {
            var preparedClaims = new List<Claim>(claims.Count);
            var claimSourceTypes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var claim in claims)
            {
                if (TokenGeneratedClaimTypes.Contains(claim.Type))
                {
                    continue;
                }

                var canonicalType = ClaimTypeMapping.OutboundClaimTypeMap.TryGetValue(claim.Type, out var outboundType)
                    ? outboundType
                    : claim.Type;
                var key = string.Concat(canonicalType, "\0", claim.Value, "\0", claim.ValueType);
                if (!claimSourceTypes.TryGetValue(key, out var sourceType))
                {
                    claimSourceTypes.Add(key, claim.Type);
                    preparedClaims.Add(claim);
                }
                else if (string.Equals(sourceType, claim.Type, StringComparison.Ordinal))
                {
                    preparedClaims.Add(claim);
                }
            }
            return preparedClaims;
        }

        internal static DateTimeOffset? GetAuthenticationExpiration(IEnumerable<Claim> claims)
        {
            if (claims == null)
            {
                return null;
            }

            foreach (var claim in claims)
            {
                if (claim.Type != Constants.ClaimType.AuthExpiresOn
                    || !long.TryParse(claim.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixSeconds))
                {
                    continue;
                }

                try
                {
                    return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }

            return null;
        }

        public static ClaimsPrincipal GetUserPrincipal(this OpenConnectionMessage message)
        {
#if NET6_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(message);
#else
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }
#endif

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
