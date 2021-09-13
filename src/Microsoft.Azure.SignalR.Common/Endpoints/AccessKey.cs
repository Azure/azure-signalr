// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Azure;

namespace Microsoft.Azure.SignalR
{
    internal class AccessKey
    {
        public string Id => Key?.Item1;
        public string Value => Key?.Item2;

        protected Tuple<string, string> Key { get; set; }

        public Uri Endpoint { get; }

        public AccessKey(string uri, AzureKeyCredential credential) : this(new Uri(uri), credential)
        {
        }

        public AccessKey(Uri uri, AzureKeyCredential credential) : this(uri)
        {
            Key = new Tuple<string, string>(credential.Key.GetHashCode().ToString(), credential.Key);
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
}