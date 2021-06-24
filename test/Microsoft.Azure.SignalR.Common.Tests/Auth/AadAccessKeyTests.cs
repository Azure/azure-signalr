using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    public class AadAccessKeyTests
    {
        [Fact]
        public async Task TestUpdateAccessKey()
        {
            var credential = new EnvironmentCredential();
            var endpoint = "http://localhost";
            var key = new AadAccessKey(credential, endpoint, 80);

            var audience = "http://localhost/chat";
            var claims = Array.Empty<Claim>();
            var lifetime = TimeSpan.FromHours(1);
            var algorithm = AccessTokenAlgorithm.HS256;

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm, cts.Token)
            );

            var (kid, accessKey) = ("foo", "This accesskey is a long string.");
            key.UpdateAccessKey(kid, accessKey);

            var token = await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm);
            Assert.NotNull(token);
        }

        [Fact]
        public async Task TestInitializeFailed()
        {
            var credential = new EnvironmentCredential();
            var endpoint = "http://localhost";
            var key = new AadAccessKey(credential, endpoint, 80);

            var audience = "http://localhost/chat";
            var claims = Array.Empty<Claim>();
            var lifetime = TimeSpan.FromHours(1);
            var algorithm = AccessTokenAlgorithm.HS256;

            var task = Assert.ThrowsAsync<AzureSignalRAccessTokenNotAuthorizedException>(
                async () => await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm)
            );

            await Assert.ThrowsAnyAsync<Exception>(
                async () => await key.UpdateAccessKeyAsync()
            );

            await task;
        }
    }
}
