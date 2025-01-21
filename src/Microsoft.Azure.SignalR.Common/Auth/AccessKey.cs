// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal class AccessKey : IAccessKey
{
    public string Kid { get; }

    public byte[] KeyBytes { get; }

    public AccessKey(string key)
    {
        Kid = key.GetHashCode().ToString();
        KeyBytes = Encoding.UTF8.GetBytes(key);
    }

    public virtual Task<string> GenerateAccessTokenAsync(string audience,
                                                         IEnumerable<Claim> claims,
                                                         TimeSpan lifetime,
                                                         AccessTokenAlgorithm algorithm,
                                                         CancellationToken ctoken = default)
    {
        var token = AuthUtility.GenerateAccessToken(KeyBytes, Kid, audience, claims, lifetime, algorithm);
        return Task.FromResult(token);
    }
}