// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;

namespace AspNet.ChatSample.CSharpClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var url = "http://localhost:8009";
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

        private enum Mode
        {
            Broadcast,
            Echo,
        }

        private static async Task<IHubProxy> ConnectAsync(string url)
        {
            var writer = Console.Out;
            var connection = new HubConnection(url)
            {
                TraceWriter = writer,
                TraceLevel = TraceLevels.StateChanges
            };

            connection.Closed += () =>
            {
                Console.WriteLine($"{connection.ConnectionId} is closed");
            };

            connection.Error += e =>
            {
                Console.WriteLine(e);
            };

            var hubProxy = connection.CreateHubProxy("ChatHub");
            hubProxy.On<string, string>("BroadcastMessage", BroadcastMessage);
            hubProxy.On<string>("Echo", Echo);

            await connection.Start();

            return hubProxy;
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
