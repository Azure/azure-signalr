// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Logging;
using Microsoft.Azure.SignalR.Common;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.SignalR
{
    internal static class AuthUtility
    {
        private const int MaxTokenLength = 4096;

        private static readonly JwtSecurityTokenHandler JwtTokenHandler = new JwtSecurityTokenHandler();

        public static string GenerateJwtBearer(
            string issuer = null,
            string audience = null,
            IEnumerable<Claim> claims = null,
            DateTime? expires = null,
            AccessKey signingKey = null,
            DateTime? issuedAt = null,
            DateTime? notBefore = null,
            AccessTokenAlgorithm algorithm = AccessTokenAlgorithm.HS256)
        {
            var subject = claims == null ? null : new ClaimsIdentity(claims);
            var writer = new JwtBuilder(Encoding.UTF8.GetBytes(signingKey.Value), 512, signingKey.Id, algorithm);

            // add claims
            writer.AddClaims(claims);

            // issuer
            if (!string.IsNullOrEmpty(issuer))
            {
                writer.AddClaim(JwtBuilder.Iss, issuer);
            }
            // audience
            if (!string.IsNullOrEmpty(audience))
            {
                writer.AddClaim(JwtBuilder.Aud, audience);
            }
            

            DateTime utcNow = DateTime.UtcNow;
            if (!expires.HasValue)
            {
                expires = utcNow + TimeSpan.FromMinutes(9 * 60);
            }

            if (!issuedAt.HasValue)
            {
                issuedAt = utcNow;
            }

            if (!notBefore.HasValue)
            {
                notBefore = utcNow;
            }
            if (notBefore.Value >= expires.Value)
            {
                throw LogHelper.LogExceptionMessage(new ArgumentException(LogHelper.FormatInvariant("IDX12401: Expires: '{0}' must be after NotBefore: '{1}'.", expires.Value, notBefore.Value)));
            }
            
            writer.AddClaim(JwtBuilder.Nbf, EpochTime.GetIntDate(notBefore.Value.ToUniversalTime()));
            writer.AddClaim(JwtBuilder.Exp, EpochTime.GetIntDate(expires.Value.ToUniversalTime()));
            writer.AddClaim(JwtBuilder.Iat, EpochTime.GetIntDate(issuedAt.Value.ToUniversalTime()));

            var token = writer.BuildString();
            return token;
        }

        public static string GenerateAccessToken(
            AccessKey signingKey,
            string audience,
            IEnumerable<Claim> claims,
            TimeSpan lifetime,
            AccessTokenAlgorithm algorithm)
        {
            var expire = DateTime.UtcNow.Add(lifetime);

            var jwtToken = GenerateJwtBearer(
                audience: audience,
                claims: claims,
                expires: expire,
                signingKey: signingKey,
                algorithm: algorithm
            );

            if (jwtToken.Length > MaxTokenLength)
            {
                throw new AzureSignalRAccessTokenTooLongException();
            }

            return jwtToken;
        }

        public static string GenerateRequestId()
        {
            return Convert.ToBase64String(BitConverter.GetBytes(Stopwatch.GetTimestamp()));
        }

        private static string GetSecurityAlgorithm(AccessTokenAlgorithm algorithm)
        {
            return algorithm == AccessTokenAlgorithm.HS256 ?
                SecurityAlgorithms.HmacSha256 :
                SecurityAlgorithms.HmacSha512;
        }
    }

}
