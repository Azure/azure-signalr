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
            string requestId = null)
        {
            var requestIdClaim = new Claim[] { new Claim(Constants.ClaimType.Id, requestId ?? GenerateRequestId()) };
            var claimsWithRequestId = claims == null ? requestIdClaim : claims.Concat(requestIdClaim);
            var subject = new ClaimsIdentity(claimsWithRequestId);
            return GenerateJwtBearer(issuer, audience, subject, expires, signingKey);
        }

        public static string GenerateAccessToken(string signingKey, string audience, IEnumerable<Claim> claims, TimeSpan lifetime, string requestId = null)
        {
            var expire = DateTime.UtcNow.Add(lifetime);

            var jwtToken = GenerateJwtBearer(
                audience: audience,
                claims: claims,
                expires: expire,
                signingKey: signingKey,
                requestId: requestId
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
            string signingKey = null)
        {
            SigningCredentials credentials = null;
            if (!string.IsNullOrEmpty(signingKey))
            {
                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
                credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            }

            var token = JwtTokenHandler.CreateJwtSecurityToken(
                issuer: issuer,
                audience: audience,
                subject: subject,
                expires: expires,
                signingCredentials: credentials);
            return JwtTokenHandler.WriteToken(token);
        }
    }
}
