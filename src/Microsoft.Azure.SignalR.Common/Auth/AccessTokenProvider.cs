// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class AccessTokenProvider
    {
        private readonly AccessKey _accessKey;

        private readonly string _audience;

        private readonly IEnumerable<Claim> _claims;

        private readonly TimeSpan _lifetime;

        private readonly AccessTokenAlgorithm _algorithm;

        public AuthType AuthType => _accessKey switch
        {
            AadAccessKey => AuthType.AzureAD,
            AccessKey => AuthType.Local,
        };

        public AccessTokenProvider(AccessKey accessKey,
                                   string audience,
                                   IEnumerable<Claim> claims,
                                   TimeSpan lifetime,
                                   AccessTokenAlgorithm algorithm)
        {
            _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
            _audience = audience ?? throw new ArgumentNullException(nameof(audience));
            _claims = claims ?? throw new ArgumentNullException(nameof(claims));
            _lifetime = lifetime;
            _algorithm = algorithm;
        }

        public AccessTokenProvider(AadAccessKey accessKey)
        {
            _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        }

        public Task<string> ProvideAsync()
        {
            return _accessKey switch
            {
                AadAccessKey aad => aad.GenerateAadTokenAsync(),
                AccessKey key => key.GenerateAccessTokenAsync(_audience, _claims, _lifetime, _algorithm)
            };
        }
    }
}
