// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*------------------------------------------------------------------------------
 * Simplified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtSecurityTokenHandler.cs#L487
 * Compared with original code:
 *      1. Remove useless methods
 *      2. Remove code related with `subject.Actor` because this property is always null
 *         if (subject?.Actor != null)
               payload.AddClaim(new Claim(JwtRegisteredClaimNames.Actort, CreateActorValue(subject.Actor)));
 *      3. Change class `JwtSecurityTokenHandlerSignalR` to `public` while class `JwtSecurityTokenHandler` is `internal`
 *      4. Simplify method `CreateJwtSecurityToken`. Comments are shown above the method
 *      5. Use a simpler way for JWT token signature encryption in method `CreateJwtSecurityToken`
------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Microsoft.Azure.SignalR;

internal class SignalRJwtSecurityTokenHandler
{
    private static readonly Dictionary<string, string> OutboundClaimTypeMap = new Dictionary<string, string>(ClaimTypeMapping.OutboundClaimTypeMap);

    // Simplified from following codes:
    //      method `CreateJwtSecurityToken` in [JwtSecruityTokenHandler.cs](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtSecurityTokenHandler.cs#L487)
    //      method `CreateJwtSecurityTokenPrivate` in [JwtSecurityTokenHandler.cs](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtSecurityTokenHandler.cs#L616)
    public string CreateJwtSecurityToken(DateTime? notBefore = null,
                                         DateTime? expires = null,
                                         DateTime? issuedAt = null,
                                         string issuer = null,
                                         string audience = null,
                                         ClaimsIdentity subject = null,
                                         byte[] key = null,
                                         string kid = null,
                                         AccessTokenAlgorithm algorithm = AccessTokenAlgorithm.HS256)
    {
        if (!expires.HasValue || !issuedAt.HasValue || !notBefore.HasValue)
        {
            var now = DateTime.UtcNow;
            if (!expires.HasValue)
            {
                expires = now + TimeSpan.FromMinutes(60);
            }

            if (!issuedAt.HasValue)
            {
                issuedAt = now;
            }

            if (!notBefore.HasValue)
            {
                notBefore = now;
            }
        }

        var payload = new JwtPayload(issuer, audience, subject == null ? null : OutboundClaimTypeTransform(subject.Claims), notBefore, expires, issuedAt);
        var header = new JwtHeader(kid, algorithm);

        var rawHeader = header.Base64UrlEncode();
        var rawPayload = payload.Base64UrlEncode();
        var message = string.Concat(header.Base64UrlEncode(), ".", payload.Base64UrlEncode());

        var rawSignature = string.Empty;

        // Use a much simpler way for signature encryption than Package System.IdentityModel.Tokens.Jwt
        if (key != null)
        {
            HMAC hash = algorithm switch
            {
                AccessTokenAlgorithm.HS256 => new HMACSHA256(key),
                AccessTokenAlgorithm.HS512 => new HMACSHA512(key),
                _ => throw new NotSupportedException("Unsupported Encryption Algorithm for JWT Token"),
            };
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var hashed = hash.ComputeHash(messageBytes, 0, messageBytes.Length);
            rawSignature = Base64UrlEncoder.Encode(hashed);
        }

        if (header == null)
        {
            throw LogHelper.LogArgumentNullException(nameof(header));
        }

        if (payload == null)
        {
            throw LogHelper.LogArgumentNullException(nameof(payload));
        }

        if (string.IsNullOrWhiteSpace(rawHeader))
        {
            throw LogHelper.LogArgumentNullException(nameof(rawHeader));
        }

        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            throw LogHelper.LogArgumentNullException(nameof(rawPayload));
        }

        if (rawSignature == null)
        {
            throw LogHelper.LogArgumentNullException(nameof(rawSignature));
        }

        return string.Concat(message, ".", rawSignature);
    }

    private static IEnumerable<Claim> OutboundClaimTypeTransform(IEnumerable<Claim> claims)
    {
        foreach (var claim in claims)
        {
            if (OutboundClaimTypeMap.TryGetValue(claim.Type, out var type))
            {
                yield return new Claim(type, claim.Value, claim.ValueType, claim.Issuer, claim.OriginalIssuer, claim.Subject);
            }
            else
            {
                yield return claim;
            }
        }
    }
}
