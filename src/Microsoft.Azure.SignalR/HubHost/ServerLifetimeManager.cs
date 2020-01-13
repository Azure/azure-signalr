using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.SignalR
{
    internal class ServerLifetimeManager
    {
        private readonly IList<Func<Task>> _shutdownHooks = new List<Func<Task>>();

        private object _lock = new object();

        public ServerLifetimeManager(
            IServiceProvider provider
        )
        {
#if NETCOREAPP
            var lifetime = provider.GetService<IHostApplicationLifetime>();
#elif NETSTANDARD
            var lifetime = provider.GetService<IApplicationLifetime>();
#else
            var lifetime = null;
#endif

            lifetime?.ApplicationStopping.Register(Shutdown);
        }

        internal void Register(Func<Task> func)
        {
            lock (_lock)
            {
                _shutdownHooks.Add(func);
            }
        }

        private void Shutdown()
        {
            lock (_lock)
            {
                Task.WaitAll(_shutdownHooks.Select(func => func()).ToArray());
            }
        }
    }
}
