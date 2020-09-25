using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace ChatSample.CSharpClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var url = "http://localhost:5050";
            var currentUser = "DefaultUser";//Guid.NewGuid().ToString("N");
            var proxy = await ConnectAsync(url + "/chat?user=" + currentUser + "&endpoint=primary", Console.Out);

            Mode mode = Mode.Broadcast;
            if (args.Length > 0)
            {
                Enum.TryParse(args[0], true, out mode);
            }

            Console.WriteLine("[Application Layer]\t" + $"Logged in as user {currentUser}");
            var input = Console.ReadLine();
            while (!string.IsNullOrEmpty(input))
            {
                switch (mode)
                {
                    case Mode.Broadcast:
                        await proxy.InvokeAsync("BroadcastMessage", currentUser, input);
                        break;
                    case Mode.Echo:
                        await proxy.InvokeAsync("echo", currentUser, input);
                        break;
                    default:
                        break;
                }

                input = Console.ReadLine();
            }
        }

        private static async Task<HubConnection> ConnectAsync(string url, TextWriter output, CancellationToken cancellationToken = default)
        {
               var connection = new HubConnectionBuilder()
                .WithUrl(url)
                .AddMessagePackProtocol()
                .AddReloadFeature()
                .Build();

            connection.On<string, string>("broadcastMessage", BroadcastMessage);
            connection.On<string, string>("echo", Echo);

            connection.Closed += async (e) =>
            {
                output.WriteLine(e);
                await DelayRandom(200, 1000);
                //await StartAsyncWithRetry(connection, output, cancellationToken);
            };

            await StartAsyncWithRetry(connection, output, cancellationToken);

            return connection;
        }

        private static async Task StartAsyncWithRetry(HubConnection connection, TextWriter output, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await connection.StartAsync(cancellationToken);
                    return;
                }
                catch (Exception e)
                {
                    output.WriteLine($"Error starting: {e.Message}, retry...");
                    await DelayRandom(200, 1000);
                }
            }
        }

        /// <summary>
        /// Delay random milliseconds
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        private static Task DelayRandom(int min, int max)
        {
            return Task.Delay(StaticRandom.Next(min, max));
        }

        private static void BroadcastMessage(string name, string message)
        {
            Console.WriteLine($"{name}: {message}");
        }

        private static void Echo(string name, string message)
        {
            Console.WriteLine("[Application Layer]\t" + $"{name}: {message}");
            //Console.WriteLine(message);
        }

        private enum Mode
        {
            Broadcast,
            Echo,
        }
    }
}
