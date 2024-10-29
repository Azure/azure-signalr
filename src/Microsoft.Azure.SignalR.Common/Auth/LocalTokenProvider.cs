// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal class LocalTokenProvider : IAccessTokenProvider
{
    private readonly AccessKey _accessKey;

    private readonly AccessTokenAlgorithm _algorithm;

    private readonly string _audience;

    private readonly TimeSpan _tokenLifetime;

    private readonly IEnumerable<Claim> _claims;

    public LocalTokenProvider(
        AccessKey accessKey,
        string audience,
        IEnumerable<Claim> claims,
        AccessTokenAlgorithm algorithm = AccessTokenAlgorithm.HS256,
        TimeSpan? tokenLifetime = null)
    {
        _accessKey = accessKey ?? throw new ArgumentNullException(nameof(accessKey));
        _algorithm = algorithm;
        _audience = audience;
        _claims = claims;
        _tokenLifetime = tokenLifetime ?? Constants.Periods.DefaultAccessTokenLifetime;
    }

    public Task<string> ProvideAsync() => _accessKey.GenerateAccessTokenAsync(_audience, _claims, _tokenLifetime, _algorithm);
}
