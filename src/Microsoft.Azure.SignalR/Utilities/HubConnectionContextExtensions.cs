using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;

namespace Microsoft.Azure.SignalR
{
    public static class HubConnectionContextExtensions
    {
        public static Task ReturnResultAsync(this HubConnectionContext connection, HubInvocationMessage message)
        {
            if (message == null) return Task.CompletedTask;
            message.AddAction(nameof(HubLifetimeManager<Hub>.SendConnectionAsync))
                .AddConnectionId(connection.ConnectionId);
            return connection.WriteAsync(message);
        }
    }
}
