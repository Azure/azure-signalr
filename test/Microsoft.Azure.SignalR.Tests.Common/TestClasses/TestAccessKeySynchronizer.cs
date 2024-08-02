using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal class TestAccessKeySynchronizer : IAccessKeySynchronizer
{
    public static readonly IAccessKeySynchronizer Instance = new TestAccessKeySynchronizer();

    public void UpdateServiceEndpoints(IEnumerable<ServiceEndpoint> endpoints)
    {
        return;
    }

    public void AddServiceEndpoint(ServiceEndpoint endpoint)
    {
        return;
    }
}
