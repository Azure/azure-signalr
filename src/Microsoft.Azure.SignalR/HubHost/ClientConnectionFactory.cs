using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class ClientConnectionFactory : IClientConnectionFactory
    {
        public ServiceConnectionContext CreateConnection(OpenConnectionMessage message)
        {
            return new ServiceConnectionContext(message);
        }
    }
}
