using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.SignalR
{
    internal class ServerLifetimeManager
    {
        private readonly ConcurrentBag<Func<Task>> _shutdownHooks = new ConcurrentBag<Func<Task>>();

        public ServerLifetimeManager(
            IServiceProvider provider
        )
        {
#if NETCOREAPP
            var lifetime = provider.GetService<IHostApplicationLifetime>();
#elif NETSTANDARD
            var lifetime = provider.GetService<IApplicationLifetime>();
#endif
            lifetime?.ApplicationStopping.Register(Shutdown);
        }

        internal void Register(Func<Task> func)
        {
            _shutdownHooks.Add(func);
        }

        private void Shutdown()
        {
            Task.WaitAll(_shutdownHooks.Select(func => func()).ToArray());
        }
    }
}
