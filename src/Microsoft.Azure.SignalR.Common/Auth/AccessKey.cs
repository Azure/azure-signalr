// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal class AccessKey
{
    public string Id => Key?.Item1;

    public string Value => Key?.Item2;

    public Uri Endpoint { get; }

    protected Tuple<string, string> Key { get; set; }

    public AccessKey(string uri, string key) : this(new Uri(uri))
    {
        Key = new Tuple<string, string>(key.GetHashCode().ToString(), key);
    }

    public AccessKey(Uri uri, string key) : this(uri)
    {
        Key = new Tuple<string, string>(key.GetHashCode().ToString(), key);
    }

    protected AccessKey(Uri uri)
    {
        Endpoint = uri;
    }

    public virtual Task<string> GenerateAccessTokenAsync(
        string audience,
        IEnumerable<Claim> claims,
        TimeSpan lifetime,
        AccessTokenAlgorithm algorithm,
        CancellationToken ctoken = default)
    {
        var token = AuthUtility.GenerateAccessToken(this, audience, claims, lifetime, algorithm);
        return Task.FromResult(token);
    }
}