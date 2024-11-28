// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR;

internal abstract class AccessKey
{
    public abstract string Kid { get; }

    public abstract byte[] KeyBytes { get; }

    public abstract Uri Endpoint { get; }

    public abstract Task<string> GenerateAccessTokenAsync(string audience,
                                                          IEnumerable<Claim> claims,
                                                          TimeSpan lifetime,
                                                          AccessTokenAlgorithm algorithm,
                                                          CancellationToken ctoken = default);
}
