using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    public class ServiceConnectionTests
    {
        private const string SigningKey = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        private const string AadConnectionString = "endpoint=https://localhost;authType=aad;";

        private const string keyConnectionString = "endpoint=https://localhost;accessKey=" + SigningKey;

        [Theory]
        [InlineData(typeof(AccessKey), keyConnectionString)]
        [InlineData(typeof(AadAccessKey), AadConnectionString)]
        public async Task TestHandleKeyMessage(Type type, string connectionString)
        {
            var endpoint = new ServiceEndpoint(connectionString);
            Assert.Equal(type.Name, endpoint.AccessKey.GetType().Name);

            var hubEndpoint = new HubServiceEndpoint("foo", null, endpoint);
            var conn = new StrongServiceConnectionContainer(null, 0, hubEndpoint, NullLogger.Instance);

            var message = new AccessKeyResponseMessage()
            {
                Kid = "foo",
                AccessKey = SigningKey
            };
            await conn.HandleKeyAsync(message);

            var audience = "http://localhost/chat";
            var claims = Array.Empty<Claim>();
            var lifetime = TimeSpan.FromHours(1);
            var algorithm = AccessTokenAlgorithm.HS256;

            var clientToken = await endpoint.AccessKey.GenerateAccessTokenAsync(audience, claims, lifetime, algorithm);
            Assert.NotNull(clientToken);
        }
    }
}
