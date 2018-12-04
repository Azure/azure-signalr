using System;
using System.Collections.Generic;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Azure.SignalR.ServerlessAgent
{
    public class AccessToken
    {
        public JwtSecurityToken Token { get; private set; }

        public AccessToken(string token)
        {
            Token = ParseToken(token);
        }

        private JwtSecurityToken ParseToken(string jwtInput)
        {
            var jwtHandler = new JwtSecurityTokenHandler();

            // Check if readable token (string is in a JWT format)
            var readableToken = jwtHandler.CanReadToken(jwtInput);

            if (!readableToken)
            {
                throw new Exception("Cannot parse access token");
            }

            //Extract the headers of the JWT
            var token = jwtHandler.ReadJwtToken(jwtInput);
            return token;
        }
    }
}
