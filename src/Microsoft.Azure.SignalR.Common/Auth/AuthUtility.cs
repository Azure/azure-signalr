// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
            JwtBuilder jwtBuilder = new JwtBuilder(
                expires: expires,
                issuedAt: issuedAt,
                issuer: issuer,
                audience: audience,
                notBefore: notBefore,
                claims: claims,
                key: Encoding.UTF8.GetBytes(signingKey.Value),
                kid: signingKey.Id,
                algorithm: algorithm);
            var token = jwtBuilder.generateToken();
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
