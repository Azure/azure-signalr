using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    public class AccessKeyTests
    {
        private const string TestEndpoint = "http://localhost";
        private readonly int? TestPort = 8080;

        [Fact]
        internal void TestConsturctor()
        {
            var key = new AccessKey("abcde", TestEndpoint, TestPort);
            Assert.NotNull(key.Id);
            Assert.NotNull(key.Value);
        }
    }
}
