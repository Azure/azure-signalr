using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    interface IClientConnectionLifetimeManager
    {
        Task WhenAllCompleted();
    }
}
