// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Client;

namespace AspNet.ChatSample.CSharpClient
{
    class Program
    {
        private const int MaxMessageCount = 30000;
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
                    case Mode.Pub:
                        {
                            string groupName = "default";
                            if (args.Length >= 2)
                            {
                                groupName = args[1];
                            }
                            for (int i = 0; i < MaxMessageCount; i++)
                            {
                                await proxy.Invoke("publish", groupName, input, i);
                                await Task.Delay(1000);
                            }
                            break;
                        }
                    case Mode.Sub:
                        {
                            string groupName = "default";
                            if (args.Length >= 2)
                            {
                                groupName = args[1];
                            }
                            await proxy.Invoke("subscribe", groupName);
                            break;
                        }
                    default:
                        break;
                }

                _ = ScanAsync();
                input = Console.ReadLine();
            }
        }

        private static ConcurrentDictionary<string, List<int>> MissingIndices = new ConcurrentDictionary<string, List<int>>();

        private static Task ScanAsync()
        {
            while (true)
            {
                Task.Delay(1000);
                foreach(var key in Messages.Keys.ToArray())
                {
                    var missing = new List<int>();
                    var val = Messages[key];
                    if (val.All(s => !s))
                    {
                        continue;
                    }

                    var first = Array.FindIndex(val, s => s);
                    var last = Array.FindLastIndex(val, s => s);
                    for (var i = first; i <= last; i++)
                    {
                        if (!val[i])
                        {
                            missing.Add(i);
                        }
                    }

                    if (missing.Count > 0)
                    {
                        MissingIndices[key] = missing;
                    }
                }

                foreach(var item in MissingIndices)
                {
                    Console.WriteLine($"Group {item.Key} missing: {string.Join(',', item.Value)}");
                }
            }
        }

        private enum Mode
        {
            Broadcast,
            Echo,
            Pub,
            Sub,
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
            hubProxy.On<string, string, int>("OnMessage", OnMessage);
            await connection.Start();

            return hubProxy;
        }

        private static ConcurrentDictionary<string, bool[]> Messages = new ConcurrentDictionary<string, bool[]>(); 

        private static void OnMessage(string group, string content, int messageIndex)
        {
            if (Messages.TryGetValue(group, out var indices))
            {
                indices[messageIndex] = true;
            }
            else
            {
                Messages[group] = new bool[MaxMessageCount];
                Messages[group][messageIndex] = true;
            }
            Console.Write($"{messageIndex}.");
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
