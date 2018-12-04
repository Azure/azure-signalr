using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public class AccessTokenGenerator
    {
        public static string GenerateAccessToken(string audience, string accessKey, TimeSpan? lifetime = null)
        {
            var expire = DateTime.UtcNow.Add(lifetime?? TimeSpan.FromHours(1));
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(accessKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            IEnumerable<Claim> claims = null;
            var userId = Guid.NewGuid().ToString();
            if (userId != null)
            {
                claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                };
            }
            JwtSecurityTokenHandler JwtTokenHandler = new JwtSecurityTokenHandler();
            var token = JwtTokenHandler.CreateJwtSecurityToken(
                issuer: null,
                audience: audience,
                subject: claims == null ? null : new ClaimsIdentity(claims),
                expires: expire,
                signingCredentials: credentials);

            return JwtTokenHandler.WriteToken(token);
        }

        public static string CenerateAccessTokenForBroadcast(string connectionString, string hubName, RestApiVersions version = RestApiVersions.V1, TimeSpan? lifetime = null)
        {
            var signalrCredential = new SignalRServiceCredential(connectionString);
            var apis = new RestApis(signalrCredential.Endpoint, hubName, version);
            var api = apis.Broadcast();
            return GenerateAccessToken(api, signalrCredential.AccessKey, lifetime);
        }
    }
}
