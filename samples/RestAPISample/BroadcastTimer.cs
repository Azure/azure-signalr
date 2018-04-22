using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR;

namespace RestAPISample
{
    public class BroadcastTimer : IDisposable
    {
        private readonly ServiceContext _hubProxy;
        private Timer _timer;
        private bool _isDisposed;

        public BroadcastTimer(string hubName)
            : this(null, hubName)
        {
        }

        public BroadcastTimer(string connectionString, string hubName)
        {
            if (connectionString == null)
            {
                _hubProxy = AzureSignalR.CreateServiceContext(hubName);
            }
            else
            {
                _hubProxy = AzureSignalR.CreateServiceContext(connectionString, hubName);
            }
        }

        public void Start()
        {
            _timer = new Timer(Run, this, 100, 60 * 1000);
            _isDisposed = false;
        }

        public void Stop()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _timer.Dispose();
            }
            _isDisposed = true;
        }

        private static void Run(object state)
        {
            _ = ((BroadcastTimer)state).Broadcast();
        }

        private async Task Broadcast()
        {
            var hubMethod = "broadcastMessage";
            var name = "_BROADCAST_";
            var message = DateTime.UtcNow;
            Console.WriteLine($"Broadcast: {hubMethod} {name} {message}");
            await _hubProxy.HubContext.Clients.All.SendAsync(hubMethod, name, message);
        }
    }
}
