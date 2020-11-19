using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR
{
    public class GracefulShutdownOptions
    {
        private readonly Dictionary<string, List<object>> _dict = new Dictionary<string, List<object>>();

        /// <summary>
        /// This mode defines the server's behavior after receiving a `Ctrl+C` (SIGINT).
        /// </summary>
        public GracefulShutdownMode Mode { get; set; } = GracefulShutdownMode.Off;

        /// <summary>
        /// Specifies the timeout of a graceful shutdown process (in seconds). 
        /// Default value is 30 seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; } = Constants.Periods.DefaultShutdownTimeout;

        public void Add<THub>(Func<IHubContext<THub>, Task> func) where THub : Hub
        {
            AddMethod<THub>(func);
        }

        public void Add<THub>(Action<IHubContext<THub>> action) where THub : Hub
        {
            AddMethod<THub>(action);
        }

        public void Add<THub>(Func<Task> func)
        {
            AddMethod<THub>(func);
        }

        public void Add<THub>(Action action)
        {
            AddMethod<THub>(action);
        }

        internal async Task OnShutdown<THub>(IHubContext<THub> context) where THub : Hub
        {
            var name = typeof(THub).Name;
            if (_dict.TryGetValue(name, out var methods))
            {
                foreach (var method in methods)
                {
                    if (method is Action<IHubContext<THub>> action)
                    {
                        action(context);
                    }
                    else if (method is Action action2)
                    {
                        action2();
                    }
                    else if (method is Func<IHubContext<THub>, Task> func)
                    {
                        await func(context);
                    }
                    else if (method is Func<Task> func2)
                    {
                        await func2();
                    }
                }
            }
        }

        private void AddMethod<THub>(object method)
        {
            if (method == null)
            {
                return;
            }

            var name = typeof(THub).Name;
            if (_dict.TryGetValue(name, out var list))
            {
                list.Add(method);
            }
            else
            {
                _dict.Add(name, new List<object>() { method });
            }
        }
    }
}
