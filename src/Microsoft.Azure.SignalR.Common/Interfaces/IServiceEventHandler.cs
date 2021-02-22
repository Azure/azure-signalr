using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    public interface IServiceEventHandler
    {
        Task HandleAsync(string connectionId, ServiceEventMessage message);
    }
}
