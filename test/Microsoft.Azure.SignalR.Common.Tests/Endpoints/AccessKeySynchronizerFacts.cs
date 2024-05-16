using System.Collections.Generic;

using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Common
{
    public class AccessKeySynchronizerFacts
    {
        private static AccessKeySynchronizer GetInstanceForTest()
        {
            return new AccessKeySynchronizer(NullLoggerFactory.Instance, false);
        }

        [Fact]
        public void AddAndRemoveServiceEndpointsTest()
        {
            var synchronizer = GetInstanceForTest();

            var endpoint1 = new TestServiceEndpoint("foo");
            var endpoint2 = new TestServiceEndpoint("foo");

            Assert.Equal(0, synchronizer.ServiceEndpointsCount());
            synchronizer.UpdateServiceEndpoints(new List<ServiceEndpoint>() { endpoint1 });
            Assert.Equal(1, synchronizer.ServiceEndpointsCount());
            synchronizer.UpdateServiceEndpoints(new List<ServiceEndpoint>() { endpoint1, endpoint2 });

            Assert.Equal(2, synchronizer.ServiceEndpointsCount());
            Assert.True(synchronizer.ContainsServiceEndpoint(endpoint1));
            Assert.True(synchronizer.ContainsServiceEndpoint(endpoint2));

            synchronizer.UpdateServiceEndpoints(new List<ServiceEndpoint>() { endpoint2 });
            Assert.Equal(1, synchronizer.ServiceEndpointsCount());
            synchronizer.UpdateServiceEndpoints(new List<ServiceEndpoint>() { });
            Assert.Equal(0, synchronizer.ServiceEndpointsCount());
        }

        [Fact]
        public void FilterAadAccessKeysTest()
        {
            var synchronizer = GetInstanceForTest();

            var tenantId = "2cc65611-689b-4081-893f-6681d45d5db6";
            var endpoint1 = new ServiceEndpoint("Endpoint=http://endpoint1.net;AccessKey=123;Version=1.0");
            var endpoint2 = new ServiceEndpoint($"Endpoint=http://endpoint2.net;AuthType=aad;ClientId=foo;ClientSecret=bar;TenantId={tenantId};Version=1.0");

            synchronizer.UpdateServiceEndpoints(new List<ServiceEndpoint>() { endpoint1 });
            Assert.Empty(synchronizer.AccessKeyForMicrosoftEntraList);

            synchronizer.UpdateServiceEndpoints(new List<ServiceEndpoint>() { endpoint1, endpoint2 });
            Assert.Single(synchronizer.AccessKeyForMicrosoftEntraList);

            synchronizer.UpdateServiceEndpoints(new List<ServiceEndpoint>() { endpoint2 });
            Assert.Single(synchronizer.AccessKeyForMicrosoftEntraList);

            synchronizer.UpdateServiceEndpoints(new List<ServiceEndpoint>() { });
            Assert.Empty(synchronizer.AccessKeyForMicrosoftEntraList);
        }
    }
}
