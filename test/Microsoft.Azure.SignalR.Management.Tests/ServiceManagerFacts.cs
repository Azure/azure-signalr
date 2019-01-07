// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceManagerFacts
    {
        private JwtSecurityTokenHandler _jwtHandler = new JwtSecurityTokenHandler();

        [Fact]
        internal void GenerateClientAccessTokenTest()
        {
            var endpoint = "https://abc";
            var accessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";

            var testConnectionString = $"Endpoint={endpoint};AccessKey={accessKey};Version=1.0;";
            var userId = "UserA";
            var hubName = "signalrbench";
            var lifeTime = TimeSpan.FromSeconds(99);
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId), new Claim("type1", "val1") };

            var serviceManagerOptions = new ServiceManagerOptions
            {
                ConnectionString = testConnectionString,
                ServiceTransportType = ServiceTransportType.Transient
            };
            var serviceManager = new ServiceManager(serviceManagerOptions);
            var tokenString = serviceManager.GenerateClientAccessToken(hubName, claims, lifeTime);
            var token = _jwtHandler.ReadJwtToken(tokenString);

            var requestId = (from claim in token.Claims
                             where claim.Type == Constants.ClaimType.Id
                             select claim.Value).FirstOrDefault();

            var expectedToken = GenerateJwtBearer($"{endpoint}/client/?hub={hubName.ToLower()}",
                new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim("type1", "val1")
                },
                token.ValidTo,
                token.ValidFrom,
                token.ValidFrom,
                accessKey,
                requestId);

            Assert.Equal(expectedToken, tokenString);
        }

        private string GenerateJwtBearer(string audience,
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

            var token = _jwtHandler.CreateJwtSecurityToken(
                issuer: null,
                audience: audience,
                subject: requestIdClaims == null && subject == null ? null : new ClaimsIdentity(subject == null ? requestIdClaims : subject.Concat(requestIdClaims)),
                notBefore: notBefore,
                expires: expires,
                issuedAt: issueAt,
                signingCredentials: credentials);
            return _jwtHandler.WriteToken(token);
        }
    }
}
