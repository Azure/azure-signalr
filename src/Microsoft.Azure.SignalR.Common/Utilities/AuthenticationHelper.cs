﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.Azure.SignalR.Common;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.SignalR
{
    internal static class AuthenticationHelper
    {
        private const int MaxTokenLength = 4096;

        private static readonly JwtSecurityTokenHandler JwtTokenHandler = new JwtSecurityTokenHandler();

        public static string GenerateJwtBearer(
            string issuer = null,
            string audience = null,
            IEnumerable<Claim> claims = null,
            DateTime? expires = null,
            string signingKey = null,
            DateTime? issuedAt = null,
            DateTime? notBefore = null,
            AccessTokenAlgorithm algorithm = AccessTokenAlgorithm.HS256)
        {
            var subject = claims == null ? null : new ClaimsIdentity(claims);
            return GenerateJwtBearer(issuer, audience, subject, expires, signingKey, issuedAt, notBefore, algorithm);
        }

        public static string GenerateAccessToken(
            string signingKey, 
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

        private static string GenerateJwtBearer(
            string issuer = null,
            string audience = null,
            ClaimsIdentity subject = null,
            DateTime? expires = null,
            string signingKey = null,
            DateTime? issuedAt = null,
            DateTime? notBefore = null,
            AccessTokenAlgorithm algorithm = AccessTokenAlgorithm.HS256)
        {
            SigningCredentials credentials = null;
            if (!string.IsNullOrEmpty(signingKey))
            {
                // Refer: https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/releases/tag/5.5.0
                // From version 5.5.0, SignatureProvider caching is turned On by default, assign KeyId to enable correct cache for same SigningKey
                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
                securityKey.KeyId = signingKey;
                credentials = new SigningCredentials(securityKey, GetSecurityAlgorithm(algorithm));
            }

            var token = JwtTokenHandler.CreateJwtSecurityToken(
                issuer: issuer,
                audience: audience,
                subject: subject,
                notBefore: notBefore,
                expires: expires,
                issuedAt: issuedAt,
                signingCredentials: credentials);
            return JwtTokenHandler.WriteToken(token);
        }

        private static string GetSecurityAlgorithm(AccessTokenAlgorithm algorithm)
        {
            return algorithm == AccessTokenAlgorithm.HS256 ?
                SecurityAlgorithms.HmacSha256 :
                SecurityAlgorithms.HmacSha512;
        }
    }
}
