// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Management;

// A simple library file to test cross project reference issue: https://github.com/Azure/azure-signalr/issues/1720
namespace ManagementPublisher
{
    internal class MessagePublisher
    {
        private const string Target = "Target";
        private const string HubName = "Chat";
        private ServiceHubContext? _hubContext;

        public async Task InitAsync(string connectionString, ServiceTransportType transportType = ServiceTransportType.Transient)
        {
            var serviceManager = new ServiceManagerBuilder().WithOptions(option =>
            {
                option.ConnectionString = connectionString;
                option.ServiceTransportType = transportType;
            })
            // Uncomment the following line to get more logs
            //.WithLoggerFactory(LoggerFactory.Create(builder => builder.AddConsole()))
            .BuildServiceManager();

            _hubContext = await serviceManager.CreateHubContextAsync(HubName, default);
        }

        public Task SendMessages(string command, string? receiver, string message)
        {
            if (_hubContext == null)
            {
                throw new ArgumentNullException(nameof(_hubContext));
            }
            switch (command)
            {
                case "broadcast":
                    return _hubContext.Clients.All.SendCoreAsync(Target, new[] { message });
                case "user":
                    var userId = receiver ?? throw new ArgumentNullException(nameof(receiver));
                    return _hubContext.Clients.User(userId).SendCoreAsync(Target, new[] { message });
                case "group":
                    var groupName = receiver ?? throw new ArgumentNullException(nameof(receiver));
                    return _hubContext.Clients.Group(groupName).SendCoreAsync(Target, new[] { message });
                default:
                    Console.WriteLine($"Can't recognize command {command}");
                    return Task.CompletedTask;
            }
        }

        public Task CloseConnection(string connectionId, string reason)
        {
            if (_hubContext == null)
            {
                throw new ArgumentNullException(nameof(_hubContext));
            }
            return _hubContext.ClientManager.CloseConnectionAsync(connectionId, reason);
        }

        public Task<bool> CheckExist(string type, string id)
        {
            if (_hubContext == null)
            {
                throw new ArgumentNullException(nameof(_hubContext));
            }
            return type switch
            {
                "connection" => _hubContext.ClientManager.ConnectionExistsAsync(id),
                "user" => _hubContext.ClientManager.UserExistsAsync(id),
                "group" => _hubContext.ClientManager.UserExistsAsync(id),
                _ => throw new NotSupportedException(),
            };
        }

        public async Task DisposeAsync()
        {
            if (_hubContext != null)
            {
                await _hubContext.DisposeAsync();
            }
        }
    }
}
