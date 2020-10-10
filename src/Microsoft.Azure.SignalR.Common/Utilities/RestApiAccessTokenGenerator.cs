﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class RestApiAccessTokenGenerator
    {
        private readonly AccessKey _accessKey;

        private readonly Claim[] _claims;

        private const AccessTokenAlgorithm DefaultAlgorithm = AccessTokenAlgorithm.HS256;

        public RestApiAccessTokenGenerator(AccessKey accessKey, string serverName = null)
        {
            serverName ??= GenerateServerName();
            _accessKey = accessKey;
            _claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, serverName)
            };
        }

        public Task<string> Generate(string audience, TimeSpan? lifetime = null)
        {
            if (_accessKey is AadAccessKey key)
            {
                return key.GenerateAadToken();
            }

            return _accessKey.GenerateAccessToken(
                audience,
                _claims,
                lifetime ?? Constants.Periods.DefaultAccessTokenLifetime,
                DefaultAlgorithm);
        }

        public static string GenerateServerName()
        {
            return $"{Environment.MachineName}_{Guid.NewGuid():N}";
        }
    }
}
