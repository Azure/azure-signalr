using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.AspNet
{
    class ServerLifetimeManager : IServerLifetimeManager
    {
        private readonly ConcurrentBag<Func<Task>> _shutdownHooks = new ConcurrentBag<Func<Task>>();

        public ServerLifetimeManager()
        {
            Console.CancelKeyPress += delegate
            {
                Shutdown();
            };
        }

        public void Register(Func<Task> func)
        {
            _shutdownHooks.Add(func);
        }

        private void Shutdown()
        {
            Task.WaitAll(_shutdownHooks.Select(func => func()).ToArray());
        }
    }
}
