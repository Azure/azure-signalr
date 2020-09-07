using CommandLine;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.E2ETest
{
    class Program
    {
        static readonly Random _rand = new Random();
        static readonly string _prefix = _rand.Next().ToString();

        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed((Options opts) =>
                {
                    for (var i = 0; i < opts.RepeatConnectionTimes; i++)
                    {
                        RunCoreAsync(opts).Wait();
                        Task.Delay(1000).Wait();
                    }
                });
        }

        static async Task RunCoreAsync(Options opts)
        {
            var connections = new List<HubConnection>();
            for (var i = 0; i < opts.ConnectionCount; i++)
            {
                connections.Add(CreateConnection(opts.Url, GetUniqueName(i), opts.Protocol));
            }

            await Task.WhenAll(
                from connection in connections
                select RandomDelayTask(connection.StartAsync(), connections.Count * 1000));

            await Task.Delay(1000);
            for (var i = 0; i < opts.RepeatSendingTimes; i++)
            {
                await TestAsync(opts.Scenario, connections);
                await Task.Delay(1000);
            }
            await Task.Delay(1000);
            await Task.WhenAll(from connection in connections select connection.StopAsync());
        }

        static async Task TestAsync(Scenario scenario, IList<HubConnection> connections)
        {
            var groups = (from i in Enumerable.Range(0, connections.Count)
                          select GetUniqueName(_rand.Next(connections.Count)))
                          .OrderBy(x => _rand.Next())
                          .ToList();
            var tasks = new List<Task>();
            for (var i = 0; i < connections.Count; i++)
            {
                Task task = null;
                switch (scenario)
                {
                    case Scenario.JoinGroup:
                    case Scenario.LeaveGroup:
                        task = connections[i].SendAsync(scenario.ToString(), groups[i]);
                        break;
                    case Scenario.SendToGroupRandomly:
                        {
                            var connection = connections[i];
                            var groupName = groups[i];
                            var userName = GetUniqueName(i);
                            task = Task.Run(async () =>
                            {
                                await connection.SendAsync(Scenario.JoinGroup.ToString(), groupName);
                                await Task.Delay(1000);
                                await connection.SendAsync(Scenario.SendToGroupRandomly.ToString(), $"invoke {scenario} from user {userName}");
                                await Task.Delay(1000);
                                await connection.SendAsync(Scenario.LeaveGroup.ToString(), groupName);
                                await Task.Delay(1000);
                            });
                            break;
                        }
                    case Scenario.MessageLog:
                        {
                            var index = i;
                            var connection = connections[i];
                            var groupName = groups[i];
                            var userName = GetUniqueName(i);
                            task = Task.Run(async () =>
                            {
                                var delay = 1000;

                                // echo
                                await connection.SendAsync(Scenario.Echo.ToString(), $"invoke {scenario} from user {GetUniqueName(index)}");
                                await Task.Delay(delay);

                                // broadcast
                                await connection.SendAsync(Scenario.Broadcast.ToString(), $"invoke {scenario} from user {GetUniqueName(index)}");
                                await Task.Delay(delay);

                                // send to client
                                await connection.SendAsync(Scenario.SendToClientRandomly.ToString(), $"invoke {scenario} from user {GetUniqueName(index)}");
                                await Task.Delay(delay);

                                // send to user
                                await connection.SendAsync(Scenario.SendToUserRandomly.ToString(), $"invoke {scenario} from user {GetUniqueName(index)}");
                                await Task.Delay(delay);

                                // send to group
                                await connection.SendAsync(Scenario.JoinGroup.ToString(), groupName);
                                await Task.Delay(delay);
                                await connection.SendAsync(Scenario.SendToGroupRandomly.ToString(), $"invoke {scenario} from user {userName}");
                                await Task.Delay(delay);
                                await connection.SendAsync(Scenario.LeaveGroup.ToString(), groupName);
                                await Task.Delay(delay);
                            });
                            break;
                        }
                    default:
                        task = connections[i].SendAsync(scenario.ToString(), $"invoke {scenario} from user {GetUniqueName(i)}");
                        break;
                }
                tasks.Add(RandomDelayTask(task, connections.Count * 20));
            }

            await Task.WhenAll(tasks);
        }

        static async Task RandomDelayTask(Task task, int maxDelay)
        {
            await Task.Delay(_rand.Next(maxDelay));
            await task;
        }

        // todo: share with client and server
        static string GetUniqueName(int index)
        {
            return $"{_prefix}.{index}";
        }

        static HubConnection CreateConnection(string url, string userId, HubProtocol protocol)
        {
            var builder = new HubConnectionBuilder()
                .WithUrl($"{url}?user={userId}&prefix={_prefix}&diag=true");
            if (protocol == HubProtocol.MessagePack)
            {
                builder.AddMessagePackProtocol();
            }
            var connection = builder.Build();
            ConfigureConnection(connection);
            return connection;
        }

        static void ConfigureConnection(HubConnection connection)
        {
            Action<string> callback = (string message) =>
            {
                Console.WriteLine($"Received message {message}");
            };

            connection.On("echo", callback);
            connection.On("broadcast", callback);
            connection.On("client", callback);
            connection.On("user", callback);
            connection.On("group", callback);
        }
    }
}
