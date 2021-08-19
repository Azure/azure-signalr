using System;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests.Auth
{
    public class AccessKeyTests
    {
        [Fact]
        internal void TestConsturctor()
        {
            var endpoint = new Uri("http://localhost:8080");
            var key = new AccessKey(endpoint, "abcde");
            Assert.NotNull(key.Id);
            Assert.NotNull(key.Value);
        }
    }
}
