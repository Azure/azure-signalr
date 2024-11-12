// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Claims;
using Microsoft.Azure.SignalR.Common;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal static class AuthUtility
{
    private const int MaxTokenLength = 4096;

    private static readonly SignalRJwtSecurityTokenHandler JwtTokenHandler = new();

    public static string GenerateJwtToken(byte[] keyBytes,
                                          string? kid = null,
                                          string? issuer = null,
                                          string? audience = null,
                                          IEnumerable<Claim>? claims = null,
                                          DateTime? expires = null,
                                          DateTime? issuedAt = null,
                                          DateTime? notBefore = null,
                                          AccessTokenAlgorithm algorithm = AccessTokenAlgorithm.HS256)
    {
        var subject = claims == null ? null : new ClaimsIdentity(claims);

        var token = JwtTokenHandler.CreateJwtSecurityToken(
            expires: expires,
            issuedAt: issuedAt,
            issuer: issuer,
            audience: audience,
            notBefore: notBefore,
            subject: subject,
            key: keyBytes,
            kid: kid,
            algorithm: algorithm);

        return token;
    }

    public static string GenerateAccessToken(byte[] keyBytes,
                                             string? kid,
                                             string audience,
                                             IEnumerable<Claim> claims,
                                             TimeSpan lifetime,
                                             AccessTokenAlgorithm algorithm)
    {
        var expire = DateTime.UtcNow.Add(lifetime);

        var jwtToken = GenerateJwtToken(
            keyBytes,
            kid,
            issuer: Constants.AsrsTokenIssuer,
            audience: audience,
            claims: claims,
            expires: expire,
            algorithm: algorithm
        );

        return jwtToken.Length > MaxTokenLength ? throw new AzureSignalRAccessTokenTooLongException() : jwtToken;
    }

    public static string GenerateRequestId(string traceIdentifier)
    {
        return $"{traceIdentifier}-{Convert.ToBase64String(BitConverter.GetBytes(Stopwatch.GetTimestamp()))}";
    }
}
