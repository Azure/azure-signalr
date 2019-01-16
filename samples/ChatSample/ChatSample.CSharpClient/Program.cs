using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChatSample.CSharpClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var url = "http://localhost:5050";
            var proxy = await ConnectAsync(url);
            var currentUser = Guid.NewGuid().ToString("N");

            Mode mode = Mode.Broadcast;
            if (args.Length > 0)
            {
                Enum.TryParse(args[0], true, out mode);
            }

            Console.WriteLine($"Logged in as user {currentUser}");
            var input = Console.ReadLine();
            while (!string.IsNullOrEmpty(input))
            {
                switch (mode)
                {
                    case Mode.Broadcast:
                        await proxy.InvokeAsync("BroadcastMessage", currentUser, input);

                        break;
                    case Mode.Echo:
                        await proxy.InvokeAsync("echo", input);
                        break;
                    default:
                        break;
                }

                input = Console.ReadLine();
            }
        }

        private enum Mode
        {
            Broadcast,
            Echo,
        }

        private static async Task<HubConnection> ConnectAsync(string url)
        {
            var writer = Console.Out;
            var connection = new HubConnectionBuilder().WithUrl(url + "/chat").Build();

            connection.Closed += async (e) =>
            {
                Console.WriteLine(e);
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };

            connection.On<string, string>("BroadcastMessage", BroadcastMessage);
            connection.On<string>("Echo", Echo);

            await connection.StartAsync();

            return connection;
        }

        private static void BroadcastMessage(string name, string message)
        {
            Console.WriteLine($"{name}: {message}");
        }

        private static void Echo(string message)
        {
            Console.WriteLine(message);
        }
    }
}
