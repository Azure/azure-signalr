using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientConnectionLifetimeManager
    {
        Task WhenAllCompleted();
    }
}
