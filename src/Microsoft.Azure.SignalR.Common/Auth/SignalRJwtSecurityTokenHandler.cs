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
using System.Text;
using System.Security.Cryptography;

namespace Microsoft.Azure.SignalR
{
    internal class SignalRJwtSecurityTokenHandler
    {
        public static IDictionary<string, string> DefaultOutboundClaimTypeMap = ClaimTypeMapping.OutboundClaimTypeMap;

        private static IDictionary<string, string> _outboundClaimTypeMap = new Dictionary<string, string>(DefaultOutboundClaimTypeMap);

        // Simplified from following codes:
        //      method `CreateJwtSecurityToken` in [JwtSecruityTokenHandler.cs](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtSecurityTokenHandler.cs#L487)
        //      method `CreateJwtSecurityTokenPrivate` in [JwtSecurityTokenHandler.cs](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtSecurityTokenHandler.cs#L616)
        public string CreateJwtSecurityToken(
            DateTime? notBefore = null,
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
                DateTime now = DateTime.UtcNow;
                if (!expires.HasValue)
                    expires = now + TimeSpan.FromMinutes(60);

                if (!issuedAt.HasValue)
                    issuedAt = now;

                if (!notBefore.HasValue)
                    notBefore = now;
            }

            JwtPayload payload = new JwtPayload(issuer, audience, (subject == null ? null : OutboundClaimTypeTransform(subject.Claims)), notBefore, expires, issuedAt);
            JwtHeader header = new JwtHeader(kid, algorithm);

            string rawHeader = header.Base64UrlEncode();
            string rawPayload = payload.Base64UrlEncode();
            string message = string.Concat(header.Base64UrlEncode(), ".", payload.Base64UrlEncode());

            string rawSignature = string.Empty;

            // Use a much simpler way for signature encryption than Package System.IdentityModel.Tokens.Jwt
            if (key != null)
            {
                HMAC hash;
                switch (algorithm)
                {
                    case AccessTokenAlgorithm.HS256:
                        hash = new HMACSHA256(key);
                        break;
                    case AccessTokenAlgorithm.HS512:
                        hash = new HMACSHA512(key);
                        break;
                    default:
                        throw new NotSupportedException("Unsupported Encryption Algorithm for JWT Token");
                }
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] hashed = hash.ComputeHash(messageBytes, 0, messageBytes.Length);
                rawSignature = Base64UrlEncoder.Encode(hashed);
            }

            if (header == null)
                throw LogHelper.LogArgumentNullException(nameof(header));

            if (payload == null)
                throw LogHelper.LogArgumentNullException(nameof(payload));

            if (string.IsNullOrWhiteSpace(rawHeader))
                throw LogHelper.LogArgumentNullException(nameof(rawHeader));

            if (string.IsNullOrWhiteSpace(rawPayload))
                throw LogHelper.LogArgumentNullException(nameof(rawPayload));

            if (rawSignature == null)
                throw LogHelper.LogArgumentNullException(nameof(rawSignature));

            return string.Concat(message, ".", rawSignature);
        }

        private static IEnumerable<Claim> OutboundClaimTypeTransform(IEnumerable<Claim> claims)
        {
            foreach (Claim claim in claims)
            {
                string type = null;
                if (_outboundClaimTypeMap.TryGetValue(claim.Type, out type))
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
}
