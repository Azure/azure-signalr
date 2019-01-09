// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestApiAccessTokenGenerator
    {
        private static string _accessKey;
        private static string _userId;

        public RestApiAccessTokenGenerator(string accessKey)
        {
            _accessKey = accessKey;
            _userId = GenerateServerName();
        }

        public string Generate(string audience, TimeSpan? lifetime = null)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, _userId)
            };
            return AuthenticationHelper.GenerateAccessToken(_accessKey, audience, claims, lifetime ?? Constants.DefaultAccessTokenLifetime);
        }

        private static string GenerateServerName()
        {
            return $"{Environment.MachineName}_{Guid.NewGuid():N}";
        }
    }
}
