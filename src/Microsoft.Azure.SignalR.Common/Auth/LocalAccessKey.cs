// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal class LocalAccessKey : AccessKey
{
    public override string Kid { get; }

    public override byte[] KeyBytes { get; }

    public override Uri Endpoint { get; }

    public LocalAccessKey(Uri uri, string key) : this(uri)
    {
        Kid = key.GetHashCode().ToString();
        KeyBytes = Encoding.UTF8.GetBytes(key);
    }

    private LocalAccessKey(Uri uri)
    {
        Endpoint = uri;
    }

    public override Task<string> GenerateAccessTokenAsync(string audience,
                                                          IEnumerable<Claim> claims,
                                                          TimeSpan lifetime,
                                                          AccessTokenAlgorithm algorithm,
                                                          CancellationToken ctoken = default)
    {
        var token = AuthUtility.GenerateAccessToken(KeyBytes, Kid, audience, claims, lifetime, algorithm);
        return Task.FromResult(token);
    }
}