// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR.Management
{
    internal class RestApiAccessTokenGenerator
    {
        private string _accessKey;
        private Claim[] _claims;

        public RestApiAccessTokenGenerator(string accessKey)
        {
            _accessKey = accessKey;
            _claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, GenerateServerName())
            }; ;
        }

        public string Generate(string audience, TimeSpan? lifetime = null)
        {
            return AuthenticationHelper.GenerateAccessToken(_accessKey, audience, _claims, lifetime ?? Constants.DefaultAccessTokenLifetime);
        }

        private static string GenerateServerName()
        {
            return $"{Environment.MachineName}_{Guid.NewGuid():N}";
        }
    }
}
