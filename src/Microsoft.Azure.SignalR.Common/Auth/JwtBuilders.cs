// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class JwtBuilder
    {
        // Registered claims
        public static string Nbf => "nbf";
        public static string Exp => "exp";
        public static string Iat => "iat";
        public static string Aud => "aud";
        public static string Sub => "sub";
        public static string Iss => "iss";
        public static string Jti => "jti";

        private readonly byte[] _key;
        private readonly JwtData _jwtHeader;
        private readonly JwtData _jwtPayload;
        private readonly AccessTokenAlgorithm _algorithm;

        public JwtBuilder(
            DateTime? notBefore = null, 
            DateTime? expires = null, 
            DateTime? issuedAt = null, 
            string issuer = null, 
            string audience = null, 
            IEnumerable<Claim> claims = null, 
            byte[] key = null, 
            string kid = null, 
            AccessTokenAlgorithm algorithm = AccessTokenAlgorithm.HS256)
        {
            _key = key;
            _algorithm = algorithm;
            _jwtHeader = GenerateJwtHeader(kid, _algorithm);
            _jwtPayload = GenerateJwtPayload(issuer, audience, claims, notBefore, expires, issuedAt);

        }

        /// <summary>
        /// Create a <see cref="JwtData"/> representing JWT payload according to registered claims and <paramref name="claims"/> which represents a series of claims.
        /// </summary>
        /// <returns>a <see cref="JwtData"/> representing JWT payload</returns>
        private JwtData GenerateJwtPayload(string issuer = null, string audience = null, IEnumerable<Claim> claims = null, DateTime ?notBefore = null, DateTime ?expires = null, DateTime ?issuedAt = null)
        {
            JwtData jwtPayload = new JwtData();

            var subject = claims == null ? null : new ClaimsIdentity(claims);
            // add claims
            jwtPayload.AddClaims(claims);

            // issuer
            if (!string.IsNullOrEmpty(issuer))
            {
                jwtPayload[Iss] = issuer;
            }
            // audience
            if (!string.IsNullOrEmpty(audience))
            {
                jwtPayload[Aud] = audience;
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

            jwtPayload[Nbf] = EpochTime.GetIntDate(notBefore.Value.ToUniversalTime());
            jwtPayload[Exp] = EpochTime.GetIntDate(expires.Value.ToUniversalTime());
            jwtPayload[Iat] = EpochTime.GetIntDate(issuedAt.Value.ToUniversalTime());
            return jwtPayload;
        }


        /// <summary>
        /// Create a <see cref="JwtData"/> representing JWT header "alg":<paramref name="algorithm"/>,"typ":"JWT","kid":<paramref name="kid"/>}
        /// </summary>
        /// <returns>a <see cref="JwtData"/> representing JWT header</returns>
        private JwtData GenerateJwtHeader(string kid, AccessTokenAlgorithm algorithm)
        {
            JwtData jwtHeader = new JwtData();
            // Write parameter `alg`
            switch (algorithm)
            {
                case AccessTokenAlgorithm.HS256:
                    jwtHeader.AddClaim(new Claim("alg", "HS256"));
                    break;
                case AccessTokenAlgorithm.HS512:
                    jwtHeader.AddClaim(new Claim("alg", "HS512"));
                    break;
                default:
                    break;
            }

            // Write parameter `typ` and `kid`
            jwtHeader.AddClaim(new Claim("typ", "JWT"));
            if (kid != null)
            {
                jwtHeader.AddClaim(new Claim("kid", kid));
            }

            return jwtHeader;
        }

        /// <summary>
        /// Get complete JWT token string
        /// </summary>
        /// <returns>JWT token string</returns>
        public string generateToken()
        {
            string headerAndPayload = _jwtHeader.GetBase64UrlEncoding() + "." + _jwtPayload.GetBase64UrlEncoding() + ".";

            HMAC hash = null;
            switch (_algorithm)
            {
                case AccessTokenAlgorithm.HS256:
                    hash = new HMACSHA256(_key);
                    break;
                case AccessTokenAlgorithm.HS512:
                    hash = new HMACSHA512(_key);
                    break;
                default:
                    break;
            }
            byte[] headerAndPayloadBytes = Encoding.UTF8.GetBytes(headerAndPayload);
            byte[] hashed = hash.ComputeHash(headerAndPayloadBytes, 0, headerAndPayloadBytes.Length);
            string signature = JwtData.EncodeBytesToBase64Url(hashed);
            return headerAndPayload + signature;
        }
    }
}
