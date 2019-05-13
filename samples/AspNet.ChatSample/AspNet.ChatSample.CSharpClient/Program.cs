// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Transports;
using Microsoft.Azure.SignalR;

namespace AspNet.ChatSample.CSharpClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var url = "http://localhost:8009";
            var proxy = await ConnectAsync(url, Console.Out);
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
                        await proxy.Invoke("BroadcastMessage", currentUser, input);

                        break;
                    case Mode.Echo:
                        await proxy.Invoke("echo", input);
                        break;
                    default:
                        break;
                }

                input = Console.ReadLine();
            }
        }

        private static async Task<IHubProxy> ConnectAsync(string url, TextWriter output, CancellationToken cancellationToken = default)
        {
            var connection = new HubConnection(url)
            {
                TraceWriter = output,
                TraceLevel = TraceLevels.All
            };

            connection.Closed += () =>
            {
                output.WriteLine($"{connection.ConnectionId} is closed");
            };

            connection.Error += e =>
            {
                output.WriteLine(e);

                _ = StartAsyncWithAlwaysRetry(connection, output, DelayRandom(200, 1000), cancellationToken);
            };

            var hubProxy = connection.CreateHubProxy("ChatHub");
            hubProxy.On<string, string>("BroadcastMessage", BroadcastMessage);
            hubProxy.On<string>("Echo", Echo);

            await StartAsyncWithAlwaysRetry(connection, output, cancellationToken: cancellationToken);

            return hubProxy;
        }

        private static async Task StartAsyncWithAlwaysRetry(HubConnection connection, TextWriter output, Task startDelay = null, CancellationToken cancellationToken = default)
        {
            if (startDelay != null)
            {
                await startDelay;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await connection.Start();
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
