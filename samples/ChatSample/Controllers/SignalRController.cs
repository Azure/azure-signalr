// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.SignalR;

namespace ChatSample
{
    [Route("signalr")]
    public class SignalRController : Controller
    {
        private readonly IConnectionServiceProvider _connectionServiceProvider;

        public SignalRController(IConnectionServiceProvider connectionServiceProvider)
        {
            _connectionServiceProvider = connectionServiceProvider;
        }

        [HttpGet("{hubName}")]
        public IActionResult GenerateJwtBearer(string hubName)
        {
            return new OkObjectResult(
                new
                {
                    serviceUrl = _connectionServiceProvider.GetClientEndpoint(hubName),
                    accessToken = _connectionServiceProvider.GenerateClientAccessToken(hubName,
                        new[]
                        {
                            new Claim(ClaimTypes.Name, "username"),
                            new Claim(ClaimTypes.NameIdentifier, "userId")
                        })
                });
        }
    }
}