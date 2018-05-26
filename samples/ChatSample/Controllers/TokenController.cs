// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ChatSample
{
    [Route("token")]
    public class TokenController : Controller
    {
        private const string SigningKey = "123456789012345678901234567890";
        public const string Issuer = "ChatSample";
        public const string Audience = "ChatSample";

        private static readonly JwtSecurityTokenHandler JwtTokenHandler = new JwtSecurityTokenHandler();

        public static readonly SigningCredentials SigningCreds;

        static TokenController()
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
            SigningCreds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        }

        [HttpGet]
        public IActionResult GetToken([FromQuery] string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return BadRequest("Username is required.");
            }

            var token = JwtTokenHandler.CreateJwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                subject: new ClaimsIdentity(new [] {new Claim(ClaimTypes.NameIdentifier, username)}),
                expires: DateTime.UtcNow.AddDays(1),
                signingCredentials: SigningCreds
            );

            return Ok(JwtTokenHandler.WriteToken(token));
        }
    }
}
