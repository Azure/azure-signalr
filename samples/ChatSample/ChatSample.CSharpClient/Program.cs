// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace ChatSample.CSharpClient
{
    sealed class Program
    {
        static async Task Main(string[] args)
        {
            var url = Environment.GetEnvironmentVariable("ServerEndpoint") ?? "http://localhost:5050";
            var mode = Mode.Broadcast;
            
            // Try to parse mode from environment variable
            var modeEnv = Environment.GetEnvironmentVariable("MODE");
            if (!string.IsNullOrEmpty(modeEnv))
            {
                Enum.TryParse<Mode>(modeEnv, true, out mode);
            }
            
            var proxy = await ConnectAsync(url + "/chat", Console.Out).ConfigureAwait(false);
            var currentUser = Guid.NewGuid().ToString("N");

            if (args.Length > 0)
            {
                Enum.TryParse(args[0], true, out mode);
            }

            Console.WriteLine($"Logged in as user {currentUser}");
            if (mode == Mode.Auto)
            {
                // auto mode - runs until process is terminated
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) => 
                {
                    e.Cancel = true;
                    cts.Cancel();
                };
                
                try
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        Console.WriteLine("Broadcasting...");
                        await proxy.InvokeAsync("BroadcastMessage", currentUser, $"Current time: {DateTime.Now}").ConfigureAwait(false);
                        await Task.Delay(5000, cts.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Auto mode cancelled.");
                }
            }
            else
            {
                var input = Console.ReadLine();
                while (!string.IsNullOrEmpty(input))
                {
                    switch (mode)
                    {
                        case Mode.Broadcast:
                            await proxy.InvokeAsync("BroadcastMessage", currentUser, input).ConfigureAwait(false);
                            break;
                        case Mode.Echo:
                            await proxy.InvokeAsync("echo", input).ConfigureAwait(false);
                            break;
                        default:
                            break;
                    }

                    input = Console.ReadLine();
                }
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

            await StartAsyncWithRetry(connection, output, cancellationToken).ConfigureAwait(false);

            return connection;
        }

        private static async Task StartAsyncWithRetry(HubConnection connection, TextWriter output, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await connection.StartAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch (Exception e)
                {
                    output.WriteLine($"Error starting: {e.Message}, retry...");
                    await Task.Delay(GetRandomDelayMilliseconds(), cancellationToken).ConfigureAwait(false);
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
            Auto
        }
    }
}
