// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.Tests;

internal static class JwtTokenHelper
{
    public static readonly JwtSecurityTokenHandler JwtHandler = new JwtSecurityTokenHandler();

    public static string GenerateExpectedAccessToken(JwtSecurityToken token, string audience, AccessKey accessKey, IEnumerable<Claim> customClaims = null)
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

        var tokenString = GenerateJwtToken(
            audience, claims,
            token.ValidTo,
            token.ValidFrom,
            token.ValidFrom,
            accessKey
        );

        return tokenString;
    }

    public static string GenerateExpectedAccessToken(JwtSecurityToken token, string audience, string key, IEnumerable<Claim> customClaims = null)
    {
        return GenerateExpectedAccessToken(token, audience, new AccessKey(key), customClaims: customClaims);
    }

    public static string GenerateJwtToken(string audience,
                                          IEnumerable<Claim> subject,
                                          DateTime expires,
                                          DateTime notBefore,
                                          DateTime issueAt,
                                          IAccessKey signingKey)
    {
        return AuthUtility.GenerateJwtToken(
            signingKey.KeyBytes,
            signingKey.Kid,
            issuer: Constants.AsrsTokenIssuer,
            audience: audience,
            claims: subject,
            notBefore: notBefore,
            expires: expires,
            issuedAt: issueAt
        );
    }

    public static string GenerateJwtToken(string audience,
                                          IEnumerable<Claim> subject,
                                          DateTime expires,
                                          DateTime notBefore,
                                          DateTime issueAt,
                                          string signingKey)
    {
        return GenerateJwtToken(audience, subject, expires, notBefore, issueAt, new AccessKey(signingKey));
    }
}
