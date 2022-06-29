// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Azure.Identity;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    [Collection("Auth")]
    public class AccessKeyTests
    {
        [Fact]
        public async Task GenerateClientTokenTest()
        {
            var key = new AccessKey("http://localhost:8080", "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH");
            var lifetime = Constants.Periods.DefaultAccessTokenLifetime;
            var token = await key.GenerateAccessTokenAsync("http://localhost/livetrace", Array.Empty<Claim>(), lifetime, AccessTokenAlgorithm.HS256);
            Console.WriteLine(token);
        }

        [Fact]
        public async Task GenerateAadTokenTest()
        {
            var key = new AadAccessKey(new Uri("http://localhost:8080"), new DefaultAzureCredential());
            var token = await key.GenerateAadTokenAsync();
            Console.WriteLine(token);
        }
    }
}
