using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    public class AadAccessKeyTests
    {
        private const string SigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        [Fact]
        public async Task TestUpdateAccessKey()
        {
            var credential = new EnvironmentCredential();
            var endpoint = new Uri("http://localhost");
            var key = new AadAccessKey(endpoint, credential);

            var audience = "http://localhost/chat";
            var claims = Array.Empty<Claim>();
            var lifetime = TimeSpan.FromHours(1);
            var algorithm = AccessTokenAlgorithm.HS256;

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await Assert.ThrowsAsync<TaskCanceledException>(
                async () => await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm, cts.Token)
            );

            var (kid, accessKey) = ("foo", SigningKey);
            key.UpdateAccessKey(kid, accessKey);

            var token = await key.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm);
            Assert.NotNull(token);
        }

        [Fact]
        public async Task TestInitializeFailed()
        {
            var credential = new EnvironmentCredential();
            var endpoint = new Uri("http://localhost");
            var key = new AadAccessKey(endpoint, credential);

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
