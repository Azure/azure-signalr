// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.SignalR.Tests
{
    internal static class JwtTokenHelper
    {
        public static JwtSecurityTokenHandler JwtHandler { get; } = new JwtSecurityTokenHandler();

        public static string GenerateExpectedAccessToken(JwtSecurityToken token, string audience, string accessKey, IEnumerable<Claim> customClaims = null)
        {
            var requestId = token.Claims.FirstOrDefault(claim => claim.Type == Constants.ClaimType.Id)?.Value;

            var userClaimType = JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap[ClaimTypes.NameIdentifier];
            var userId = token.Claims.FirstOrDefault(claim => claim.Type == userClaimType)?.Value;

            var claims = new Claim[] { new Claim(ClaimTypes.NameIdentifier, userId) };
            if(customClaims != null) claims = claims.Concat(customClaims).ToArray();

            var tokenString = GenerateJwtBearer(audience, claims, token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                accessKey,
                requestId);

            return tokenString;
        }

        public static string GenerateJwtBearer(string audience,
            IEnumerable<Claim> subject,
            DateTime expires,
            DateTime notBefore,
            DateTime issueAt,
            string signingKey,
            string requestId)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var requestIdClaims = requestId == null ? null : new Claim[] { new Claim(Constants.ClaimType.Id, requestId) };

            var token = JwtHandler.CreateJwtSecurityToken(
                issuer: null,
                audience: audience,
                subject: requestIdClaims == null && subject == null ? null : new ClaimsIdentity(subject == null ? requestIdClaims : subject.Concat(requestIdClaims)),
                notBefore: notBefore,
                expires: expires,
                issuedAt: issueAt,
                signingCredentials: credentials);
            return JwtHandler.WriteToken(token);
        }
    }
}
