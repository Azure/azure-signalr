using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    public class AccessKeyTests
    {
        private const string TestClientId = "";
        private const string TestClientSecret = "";
        private const string TestTenantId = "";

        [Fact]
        public void TestConsturctor()
        {
            var key = new AccessKey("abcde");
            Assert.NotNull(key.Id);
        }

        [Fact]
        public void TestConstructorForAad()
        {
            var key = new AadAccessKey(new AadManagedIdentityOptions());
            Assert.IsAssignableFrom<AccessKey>(key);
            Assert.False(key.Authorized);
            Assert.Null(key.Id);
        }

        [Fact(Skip ="Provide valid aad options")]
        public async Task TestAuthenticateAsync()
        {
            var options = new AadApplicationOptions(TestClientId, TestTenantId).WithClientSecret(TestClientSecret);
            var key = new AadAccessKey(options);
            await key.AuthorizeAsync("http://localhost", 8080, "serverId");

            Assert.True(key.Authorized);
            Assert.NotNull(key.Id);
            Assert.NotNull(key.Value);
        }
    }
}
