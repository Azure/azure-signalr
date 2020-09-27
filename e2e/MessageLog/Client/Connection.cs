using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.E2ETest
{
    public abstract class ConnectionBase
    {
        protected const string Endpoint = "http://localhost:5000";
        protected const string Hub = "E2ETestHub";

        protected static Action<string> Callback = (string message) =>
        {
            Console.WriteLine($"Received message {message}");
        };

        protected string Prefix;
        protected string UserId;
        protected HubProtocol Protocol;

        public ConnectionBase(string prefix, string userId, HubProtocol protocol)
        {
            Prefix = prefix;
            UserId = userId;
            Protocol = protocol;
            Build();
        }

        public abstract Task StartAsync();
        public abstract Task StopAsync();
        public abstract Task SendAsync(string method, params object[] args);
        protected abstract void Build();
    }

    public class AspNetConnection : ConnectionBase
    {
        private AspNet.SignalR.Client.IHubProxy _hubProxy;
        private Microsoft.AspNet.SignalR.Client.HubConnection _connection;

        public AspNetConnection(string prefix, string userId, HubProtocol protocol) : base(prefix, userId, protocol)
        {
        }

        protected override void Build()
        {
            _connection = new Microsoft.AspNet.SignalR.Client.HubConnection($"{Endpoint}", $"user={UserId}&prefix={Prefix}&diag=true")
            {
                TraceLevel = AspNet.SignalR.Client.TraceLevels.All
            };

            _hubProxy = _connection.CreateHubProxy(Hub);
            ConfigureConnection(_hubProxy);
        }

        public override Task SendAsync(string method, params object[] args)
        {
            return _hubProxy.Invoke(method, args);
        }

        public override Task StartAsync()
        {
            return _connection.Start();
        }

        public override Task StopAsync()
        {
            _connection.Stop();
            return Task.CompletedTask;
        }

        private static void ConfigureConnection(Microsoft.AspNet.SignalR.Client.IHubProxy hubProxy)
        {
            hubProxy.On<string>("echo", Callback);
            hubProxy.On<string>("broadcast", Callback);
            hubProxy.On<string>("client", Callback);
            hubProxy.On<string>("user", Callback);
            hubProxy.On<string>("group", Callback);
        }
    }

    public class AspNetCoreConnection : ConnectionBase
    {
        private AspNetCore.SignalR.Client.HubConnection _connection;

        public AspNetCoreConnection(string prefix, string userId, HubProtocol protocol) : base(prefix, userId, protocol)
        {
        }

        protected override void Build()
        {
            var builder = new HubConnectionBuilder()
                .WithUrl($"{Endpoint}/{Hub}?user={UserId}&prefix={Prefix}&diag=true");
            if (Protocol == HubProtocol.MessagePack)
            {
                builder.AddMessagePackProtocol();
            }
            _connection = builder.Build();
            ConfigureConnection(_connection);
        }

        public override Task StartAsync()
        {
            return _connection.StartAsync();
        }

        public override Task StopAsync()
        {
            return _connection.StopAsync();
        }

        public override Task SendAsync(string method, params object[] args)
        {
            return _connection.SendCoreAsync(method, args);
        }
        private static void ConfigureConnection(AspNetCore.SignalR.Client.HubConnection connection)
        {
            connection.On("echo", Callback);
            connection.On("broadcast", Callback);
            connection.On("client", Callback);
            connection.On("user", Callback);
            connection.On("group", Callback);
        }
    }

}
