namespace Microsoft.Azure.SignalR.Tests.Common
{
    internal class TestHubServiceEndpoint : HubServiceEndpoint
    {
        public TestHubServiceEndpoint(
            string name = null,
            IServiceEndpointProvider provider = null,
            ServiceEndpoint endpoint = null
        ) : base(
            name ?? "foo",
            provider ?? new TestServiceEndpointProvider(),
            endpoint ?? new TestServiceEndpoint()
        )
        {
        }
    }
}