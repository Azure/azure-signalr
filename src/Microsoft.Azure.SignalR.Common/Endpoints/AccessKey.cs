// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal class AccessKey
    {
        public string Id { get; protected set; }

        public string Value { get; protected set; }

        public AccessKey(string key)
        {
            Id = key.GetHashCode().ToString();
            Value = key;
        }

        protected AccessKey() { }

        public virtual Task<string> GenerateAccessToken(
            string audience,
            IEnumerable<Claim> claims,
            TimeSpan lifetime,
            AccessTokenAlgorithm algorithm)
        {
            var token = AuthUtility.GenerateAccessToken(this, audience, claims, lifetime, algorithm);
            return Task.FromResult(token);
        }
    }
}