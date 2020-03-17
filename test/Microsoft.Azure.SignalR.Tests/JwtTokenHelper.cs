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
        public static readonly JwtSecurityTokenHandler JwtHandler = new JwtSecurityTokenHandler();

        public static string GenerateExpectedAccessToken(JwtSecurityToken token, string audience, string accessKey, IEnumerable<Claim> customClaims = null)
        {
            var requestId = token.Claims.FirstOrDefault(claim => claim.Type == Constants.ClaimType.Id)?.Value;

            var userClaimType = JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap[ClaimTypes.NameIdentifier];
            var userId = token.Claims.FirstOrDefault(claim => claim.Type == userClaimType)?.Value;

            var claims = new List<Claim>();
            if (userId != null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
            }
            if (customClaims != null)
            {
                claims.AddRange(customClaims.ToList());
            }

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
            
            return AuthUtility.GenerateJwtBearer(
                issuer: null,
                audience: audience,
                claims: subject,
                notBefore: notBefore,
                expires: expires,
                issuedAt: issueAt,
                signingKey: signingKey);
        }
    }
}
