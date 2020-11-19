using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    public class AccessKeyTests
    {
        private const string TestClientId = "";
        private const string TestClientSecret = "";
        private const string TestTenantId = "";

        private const string TestEndpoint = "http://localhost";
        private int? TestPort = 8080;

        [Fact]
        public void TestConsturctor()
        {
            var key = new AccessKey("abcde", TestEndpoint, TestPort);
            Assert.NotNull(key.Id);
        }

        [Fact]
        public void TestConstructorForAad()
        {
            var key = new AadAccessKey(new AadManagedIdentityOptions(), TestEndpoint, TestPort);
            Assert.IsAssignableFrom<AccessKey>(key);
            Assert.False(key.Authorized);
            Assert.Null(key.Id);
            Assert.Null(key.Value);
        }

        [Fact(Skip ="Provide valid aad options")]
        public async Task TestAuthenticateAsync()
        {
            var options = new AadApplicationOptions(TestClientId, TestTenantId).WithClientSecret(TestClientSecret);
            var key = new AadAccessKey(options, TestEndpoint, TestPort);
            await key.AuthorizeAsync("serverId");

            Assert.True(key.Authorized);
            Assert.NotNull(key.Id);
            Assert.NotNull(key.Value);
        }
    }
}
