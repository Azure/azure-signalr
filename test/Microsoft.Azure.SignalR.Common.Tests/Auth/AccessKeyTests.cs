using System;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests;
using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    public class AccessKeyTests
    {
        private const string TestClientId = "";
        private const string TestClientSecret = "";
        private const string TestTenantId = "";

        private const string TestEndpoint = "http://localhost";
        private readonly int? TestPort = 8080;

        [Fact]
        internal void TestConsturctor()
        {
            var key = new AccessKey("abcde", TestEndpoint, TestPort);
            Assert.NotNull(key.Id);
            Assert.NotNull(key.Value);
        }

        [Fact]
        internal void TestConstructorForAadApplication()
        {
            var clientId = Guid.NewGuid().ToString();
            var tenantId = Guid.NewGuid().ToString();
            var options = new AadApplicationOptions(clientId, tenantId);
            var key = new AadAccessKey(options, TestEndpoint, TestPort);
            Assert.IsAssignableFrom<AccessKey>(key);
            Assert.False(key.Authorized);
            Assert.Null(key.Id);
            Assert.Null(key.Value);
        }

        [Theory]
        [InlineData(null, ManagedIdentityType.System)]
        [InlineData("foo", ManagedIdentityType.UserAssigned)]
        internal void TestConstructorForAadManagedIdeneity(string clientId, ManagedIdentityType expectedType)
        {
            var options = clientId == null ? new AadManagedIdentityOptions() : new AadManagedIdentityOptions(clientId);
            Assert.Equal(expectedType, options.ManagedIdentityType);

            var key = new AadAccessKey(options, TestEndpoint, TestPort);
            Assert.IsAssignableFrom<AccessKey>(key);
            Assert.False(key.Authorized);
            Assert.Null(key.Id);
            Assert.Null(key.Value);
        }

        [Fact(Skip ="Provide valid aad options")]
        internal async Task TestAuthenticateAsync()
        {
            var options = new AadApplicationOptions(TestClientId, TestTenantId).WithClientSecret(TestClientSecret);
            var key = new AadAccessKey(options, TestEndpoint, TestPort);
            await key.UpdateAccessKeyAsync("serverId");

            Assert.True(key.Authorized);
            Assert.NotNull(key.Id);
            Assert.NotNull(key.Value);
        }
    }
}
