using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    interface IServerLifetimeManager
    {
        void Register(Func<Task> func);
    }
}
