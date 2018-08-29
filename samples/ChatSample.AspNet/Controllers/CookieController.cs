// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNet.Identity.Owin;
using System.Web;

namespace ChatSample.AspNet
{
    public class CookieController : ApiController
    {
        [Route("cookie/login")]
        [HttpGet()]
        public IHttpActionResult Login(string username, string role)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(role))
            {
                return BadRequest("Username and role is required.");
            }

            if (!IsExistingUser(username))
            {
                return Unauthorized();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, DefaultAuthenticationTypes.ApplicationCookie);
            var ctx = HttpContext.Current.GetOwinContext();
            var authenticationManager = ctx.Authentication;
            authenticationManager.SignIn(claimsIdentity);
            return Ok();
        }

        private bool IsExistingUser(string username)
        {
            return username.StartsWith("cookie");
        }
    }
}
