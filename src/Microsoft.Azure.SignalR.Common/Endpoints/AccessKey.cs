// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class AccessKey
    {
        public string Id => Key?.Item1;
        public string Value => Key?.Item2;

        protected Tuple<string, string> Key { get; set; }

        public string Endpoint { get; }
        public int? Port { get; }

        public AccessKey(string key, string endpoint, int? port) : this(endpoint, port)
        {
            Key = new Tuple<string, string>(key.GetHashCode().ToString(), key);
        }

        protected AccessKey(string endpoint, int? port)
        {
            Endpoint = endpoint;
            Port = port;
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
}