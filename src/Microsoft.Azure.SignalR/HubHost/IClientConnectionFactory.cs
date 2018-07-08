using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientConnectionFactory
    {
        ServiceConnectionContext CreateConnection(OpenConnectionMessage message);
    }
}
