﻿using System;
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
            var proxy = await ConnectAsync(url + "/chat", Console.Out);
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
        private static async Task<HubConnection> ConnectAsync(string url, TextWriter output, CancellationToken cancellationToken = default)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(url)
                .AddMessagePackProtocol()
                .WithAutomaticReconnect(new AlwaysRetryPolicy())
                .Build();

            connection.On<string, string>("BroadcastMessage", BroadcastMessage);
            connection.On<string>("Echo", Echo);

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
                    await Task.Delay(GetRandomDelayMilliseconds());
                }
            }
        }

        private sealed class AlwaysRetryPolicy : IRetryPolicy
        {
            public TimeSpan? NextRetryDelay(RetryContext retryContext)
            {
                return TimeSpan.FromMilliseconds(GetRandomDelayMilliseconds());
            }
        }

        private static int GetRandomDelayMilliseconds()
        {
            return StaticRandom.Next(1000, 2000);
        }

        private static void BroadcastMessage(string name, string message)
        {
            Console.WriteLine($"{name}: {message}");
        }

        private static void Echo(string message)
        {
            Console.WriteLine(message);
        }

        private enum Mode
        {
            Broadcast,
            Echo,
        }
    }
}
