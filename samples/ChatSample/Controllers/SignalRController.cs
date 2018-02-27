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
        private readonly EndpointProvider _endpointProvider;
        private readonly TokenProvider _tokenProvider;

        public SignalRController(EndpointProvider endpointProvider, TokenProvider tokenProvider)
        {
            _endpointProvider = endpointProvider;
            _tokenProvider = tokenProvider;
        }

        [HttpGet("{hubName}")]
        public IActionResult GenerateJwtBearer(string hubName)
        {
            return new OkObjectResult(
                new
                {
                    serviceUrl = _endpointProvider.GetClientEndpoint(hubName),
                    accessToken = _tokenProvider.GenerateClientAccessToken(hubName, new[]
                        {
                            new Claim(ClaimTypes.Name, "username"),
                            new Claim(ClaimTypes.NameIdentifier, "userId")
                        })
                });
        }
    }
}