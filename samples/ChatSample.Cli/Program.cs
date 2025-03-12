// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using System.Threading.Channels;

using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.SignalR.Management;

internal static partial class Program
{
    private sealed record TypeInfo(Type Type, Func<string, object> Parse);

    [GeneratedRegex(@"^connect\s+(.+?)\s+(\w+)(?:\s+(\-\-transient|\-t))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ConnectRegex();
    [GeneratedRegex(@"^define\s+(\w+)(?:\(((?:\s*\w+\s*,)*\s*\w+)?\s*\))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex DefineRegex();
    [GeneratedRegex(@"^^define\-stream\s+(\w+)(?:\(((?:\s*\w+\s*,)*\s*\w+)?\s*\))?\s*\:\s*(\w+)$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex DefineStreamRegex();
    [GeneratedRegex(@"^all\s*\.\s*(\w+)\s*(?:\((.+)\))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex BroadcastRegex();
    [GeneratedRegex(@"^group\s*\(\s*(\w+)\s*\)\s*\.\s*(\w+)\s*(?:\((.+)\))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex GroupRegex();
    [GeneratedRegex(@"^user\s*\(\s*(\w+)\s*\)\s*\.\s*(\w+)\s*(?:\((.+)\))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex UserRegex();
    [GeneratedRegex(@"^connection\s*\(\s*([\w_-]+)\s*\)\s*\.\s*(\w+)\s*(?:\((.+)\))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ConnectionRegex();
    [GeneratedRegex(@"^open\-stream\s*\(\s*([\w_-]+)\s*,\s*(\w+)\s*\)\s*\:\s*(\w+)\s*$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex OpenStreamRegex();
    [GeneratedRegex(@"^close\-stream\s*\(\s*([\w_-]+)\s*,\s*(\w+)\s*\)\s*(.*?)\s*$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex CloseStreamRegex();
    [GeneratedRegex(@"^stream\-item\s*\(\s*([\w_-]+)\s*,\s*(\w+)\s*\)\s*(\S.*?)\s*$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex StreamItemRegex();

    [GeneratedRegex(@"^client\s+(.+?)\s+(\w+)$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ClientRegex();
    [GeneratedRegex(@"^send\s+(\w+)\s*(?:\((.+)\))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ClientSendRegex();
    [GeneratedRegex(@"^stream\s+(\w+)\s*(?:\((.+)\))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ClientStreamRegex();
    [GeneratedRegex(@"^listen\s+(\w+)(?:\(((?:\s*\w+\s*,)*\s*\w+)?\s*\))?$", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex ListenRegex();

    private static readonly Dictionary<string, TypeInfo> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "int", new(typeof(int), x => int.Parse(x, null)) },
        { "string", new(typeof(string), x => x) },
        { "bool", new(typeof(bool), x => bool.Parse(x)) },
        { "double", new(typeof(double), x => double.Parse(x, null)) }
    };

    private static readonly Dictionary<string, TypeInfo[]> MethodDefineMap = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, (TypeInfo[] Params, TypeInfo Item)> StreamMethodDefineMap = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<(string ConnectionId, string StreamId), (TypeInfo TypeInfo, Channel<object> Channel)> StreamMap = [];

    private static async Task Main()
    {
        while (true)
        {
            var line = Console.ReadLine()?.Trim();
            if (line == null)
            {
                return;
            }
            if (line.Length == 0)
            {
                continue;
            }
            if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if ("help".Equals(line, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Available commands:");
                Console.WriteLine("  connect <connection-string> <hub> [--transient/-t]");
                Console.WriteLine("  client <connection-string> <hub>");
                Console.WriteLine("  help");
                Console.WriteLine("  exit");
                continue;
            }
            if ("list".Equals(line, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (m, p) in MethodDefineMap)
                {
                    Console.WriteLine($"Target: {m}, ArgTypes: {string.Join(", ", p.Select(x => x.Type.Name))}");
                }
                continue;
            }
            if (await Try(ConnectRegex(), line, Connect) ||
                await Try(ClientRegex(), line, RunAsClient))
            {
                continue;
            }

            Console.WriteLine($"Unrecognized command: {line}");
            Console.WriteLine("Type 'help' for a list of available commands.");
        }
    }

    private static async ValueTask<bool> Try(Regex regex, string line, Func<Match, Task> action)
    {
        var match = regex.Match(line);
        if (match.Success)
        {
            await action(match);
            return true;
        }
        return false;
    }

    private static async Task Connect(Match match)
    {
        var connectionString = match.Groups[1].Value;
        var hub = match.Groups[2].Value;
        var transportType = match.Groups[3].Value.Length > 0 ? ServiceTransportType.Transient : ServiceTransportType.Persistent;
        Console.WriteLine($"Connecting to {connectionString} for {hub} with transport type {transportType}");
        await ConnectAsync(
            connectionString,
            hub,
            transportType);
    }

    private static async Task ConnectAsync(string connectionString, string hub, ServiceTransportType transportType)
    {
        var serviceManager = new ServiceManagerBuilder().WithOptions(option =>
        {
            option.ConnectionString = connectionString;
            option.ServiceTransportType = transportType;
        }).BuildServiceManager();
        var hubContext = await serviceManager.CreateHubContextAsync(hub, default);
        Console.WriteLine($"Connected to {connectionString} for {hub} with transport type {transportType}");
        await CommandLoop(hubContext);
    }

    private static async Task CommandLoop(ServiceHubContext hubContext)
    {
        while (true)
        {
            var line = Console.ReadLine()?.Trim();
            if (line == null)
            {
                return;
            }
            if (line.Length == 0)
            {
                continue;
            }
            if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if (line.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Available commands:");
                Console.WriteLine("  define <target>[(<argType1>, <argType2>, ...)]");
                Console.WriteLine("  all.<target> ([<arg1>, <arg2>, ...])");
                Console.WriteLine("  group(<group>).<target> ([<arg1>, <arg2>, ...])");
                Console.WriteLine("  user(<user>).<target> ([<arg1>, <arg2>, ...])");
                Console.WriteLine("  connection(<connection-id>).<target> ([<arg1>, <arg2>, ...])");
                Console.WriteLine("  open-stream(<connection-id>, <streamId>) : <type>");
                Console.WriteLine("  close-stream(<connection-id>, <streamId>) [<error>]");
                Console.WriteLine("  stream-item(<connection-id>, <streamId>) <item>");
                Console.WriteLine("  list");
                Console.WriteLine("  list-stream");
                Console.WriteLine("  help");
                Console.WriteLine("  exit");
                continue;
            }
            if ("list".Equals(line, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (m, p) in MethodDefineMap)
                {
                    Console.WriteLine($"Target: {m}, ArgTypes: {string.Join(", ", p.Select(x => x.Type.Name))}");
                }
                continue;
            }
            if ("list-stream".Equals(line, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var (k, v) in StreamMap)
                {
                    Console.WriteLine($"Connection: {k.ConnectionId}, Stream: {k.StreamId}, Type: {v.TypeInfo.Type.Name}");
                }
                continue;
            }
            if (await Try(DefineRegex(), line, Define) ||
                await Try(BroadcastRegex(), line, m => Send(hubContext.Clients.All, m.Groups[1].Value, m.Groups[2].Value)) ||
                await Try(GroupRegex(), line, m => Send(hubContext.Clients.Group(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value)) ||
                await Try(UserRegex(), line, m => Send(hubContext.Clients.User(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value)) ||
                await Try(ConnectionRegex(), line, m => Send(hubContext.Clients.Client(m.Groups[1].Value), m.Groups[2].Value, m.Groups[3].Value)) ||
                await Try(OpenStreamRegex(), line, m => OpenStream(hubContext, m)) ||
                await Try(CloseStreamRegex(), line, CloseStream) ||
                await Try(StreamItemRegex(), line, StreamItem))
            {
                continue;
            }
            else
            {
                Console.WriteLine($"Unrecognized command: {line}");
                Console.WriteLine("Type 'help' for a list of available commands.");
                continue;
            }
        }
    }

    private static Task Define(Match match)
    {
        var target = match.Groups[1].Value;
        var paramTypes = match.Groups[2].Value.Split(',').Select(x => x.Trim()).ToList();
        if (!CheckParameter(paramTypes))
        {
            return Task.CompletedTask;
        }
        MethodDefineMap[target] = [.. paramTypes.Select(x => TypeMap[x])];
        return Task.CompletedTask;
    }

    private static Task DefineStream(Match match)
    {
        var target = match.Groups[1].Value;
        var paramTypes = match.Groups[2].Value.Split(',').Select(x => x.Trim()).ToList();
        if (!CheckParameter(paramTypes))
        {
            Console.WriteLine($"Invalid argument types: ({string.Join(", ", paramTypes)})");
            Console.WriteLine($"Supported argument types are: {string.Join(", ", TypeMap.Keys)}");
            return Task.CompletedTask;
        }
        var itemType = match.Groups[3].Value;
        if (!TypeMap.TryGetValue(itemType, out var itemTypeInfo))
        {
            Console.WriteLine($"Invalid stream item type: {itemType}, supported argument types are: {string.Join(", ", TypeMap.Keys)}");
            return Task.CompletedTask;
        }
        StreamMethodDefineMap[target] = (paramTypes.Select(x => TypeMap[x]).ToArray(), itemTypeInfo);
        return Task.CompletedTask;
    }

    private static async Task Send(IClientProxy proxy, string target, string args)
    {
        if (!MethodDefineMap.TryGetValue(target, out var argTypes))
        {
            Console.WriteLine($"Undefined target: {target}");
            Console.WriteLine($"Please define the target first.");
            return;
        }
        var argStrings = args.Split(',').Select(x => x.Trim()).ToList();

        if (argStrings.Count != argTypes.Length)
        {
            Console.WriteLine($"Invalid number of arguments for {target}: {argStrings.Count} != {argTypes.Length}");
            return;
        }
        var argValues = new object[argTypes.Length];
        var isValid = true;
        for (var i = 0; i < argTypes.Length; i++)
        {
            try
            {
                argValues[i] = argTypes[i].Parse(argStrings[i]);
            }
            catch (Exception)
            {
                Console.WriteLine($"Invalid argument type for {target}: {argValues[i]}");
                isValid = false;
                break;
            }
        }
        if (!isValid)
        {
            return;
        }
        await proxy.SendCoreAsync(target, argValues);
    }

    private static Task OpenStream(ServiceHubContext hubContext, Match match)
    {
        var connectionId = match.Groups[1].Value;
        var streamId = match.Groups[2].Value;
        var type = match.Groups[3].Value;
        if (!TypeMap.TryGetValue(type, out var info))
        {
            Console.WriteLine($"Invalid argument type: {type}");
            return Task.CompletedTask;
        }
        if (StreamMap.ContainsKey((connectionId, streamId)))
        {
            Console.WriteLine($"Stream already exists: {connectionId}, {streamId}");
            return Task.CompletedTask;
        }
        var channel = Channel.CreateUnbounded<object>();
        StreamMap.Add((connectionId, streamId), (info, channel));
        _ = hubContext.Streaming.SendStreamAsync(connectionId, streamId, channel.Reader, default);
        return Task.CompletedTask;
    }

    private static Task CloseStream(Match match)
    {
        var connectionId = match.Groups[1].Value;
        var streamId = match.Groups[2].Value;
        var error = match.Groups[3].Value;

        if (!StreamMap.TryGetValue((connectionId, streamId), out var pair))
        {
            Console.WriteLine($"Stream not found: {connectionId}, {streamId}");
            return Task.CompletedTask;
        }
        if (string.IsNullOrEmpty(error))
        {
            pair.Channel.Writer.Complete();
        }
        else
        {
#pragma warning disable CA2201 // Do not raise reserved exception types
            pair.Channel.Writer.Complete(new Exception(error));
#pragma warning restore CA2201 // Do not raise reserved exception types
        }
        StreamMap.Remove((connectionId, streamId));
        return Task.CompletedTask;
    }

    private static Task StreamItem(Match match)
    {
        var connectionId = match.Groups[1].Value;
        var streamId = match.Groups[2].Value;
        var item = match.Groups[3].Value;

        if (!StreamMap.TryGetValue((connectionId, streamId), out var pair))
        {
            Console.WriteLine($"Stream not found: {connectionId}, {streamId}");
            return Task.CompletedTask;
        }

        try
        {
            var value = pair.TypeInfo.Parse(item);
            pair.Channel.Writer.TryWrite(value);
        }
        catch (Exception)
        {
            Console.WriteLine($"Invalid argument for stream item {pair.TypeInfo.Type}: {item}");
        }
        return Task.CompletedTask;
    }

    private static async Task RunAsClient(Match match)
    {
        var connectionString = match.Groups[1].Value;
        var hub = match.Groups[2].Value;

        using var serviceManager = new ServiceManagerBuilder().WithOptions(option =>
        {
            option.ConnectionString = connectionString;
            option.ServiceTransportType = ServiceTransportType.Transient;
        }).BuildServiceManager();
        var hubContext = await serviceManager.CreateHubContextAsync(hub, default);
        var negotiateResponse = await hubContext.NegotiateAsync();

        await using var conn = new HubConnectionBuilder()
            .WithUrl(negotiateResponse.Url!, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(negotiateResponse.AccessToken);
                options.Transports = HttpTransportType.WebSockets;
            })
            .Build();
        await conn.StartAsync();
        Console.WriteLine($"Client {conn.ConnectionId} connected.");
        await ClientCommandLoop(conn);
    }

    private static async Task ClientCommandLoop(HubConnection conn)
    {
        var invocationId = 0;
        while (true)
        {
            var line = Console.ReadLine()?.Trim();
            if (line == null)
            {
                return;
            }
            if (line.Length == 0)
            {
                continue;
            }
            if (line.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            if (line.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Available commands:");
                Console.WriteLine("  define <target>[(<argType1>, <argType2>, ...)]");
                Console.WriteLine("  define-stream <target>[(<argType1>, <argType2>, ...)]:itemType");
                Console.WriteLine("  send <target> ([<arg1>, <arg2>, ...])");
                Console.WriteLine("  stream <target> ([<arg1>, <arg2>, ...])");
                Console.WriteLine("  listen <target>[(<argType1>, <argType2>, ...)]");
                Console.WriteLine("  help");
                Console.WriteLine("  exit");
                continue;
            }
            if (await Try(DefineRegex(), line, Define) ||
                await Try(DefineStreamRegex(), line, DefineStream) ||
                await Try(ClientSendRegex(), line, ClientSend) ||
                await Try(ClientStreamRegex(), line, ClientStream) ||
                await Try(ListenRegex(), line, ClientListen))
            {
                continue;
            }
            else
            {
                Console.WriteLine($"Unrecognized command: {line}");
                Console.WriteLine("Type 'help' for a list of available commands.");
                continue;
            }
        }

        async Task ClientSend(Match match)
        {
            var target = match.Groups[1].Value;
            var args = match.Groups[2].Value;
            if (!MethodDefineMap.TryGetValue(target, out var argTypes))
            {
                Console.WriteLine($"Undefined target: {target}");
                Console.WriteLine($"Please define the target first.");
                return;
            }
            var argStrings = args.Split(',').Select(x => x.Trim()).ToList();
            if (argStrings.Count != argTypes.Length)
            {
                Console.WriteLine($"Invalid number of arguments for {target}: {argStrings.Count} != {argTypes.Length}");
                return;
            }
            var argValues = new object[argTypes.Length];
            var isValid = true;
            for (var i = 0; i < argTypes.Length; i++)
            {
                try
                {
                    argValues[i] = argTypes[i].Parse(argStrings[i]);
                }
                catch (Exception)
                {
                    Console.WriteLine($"Invalid argument type for {target}: {argValues[i]}");
                    isValid = false;
                    break;
                }
            }
            if (!isValid)
            {
                return;
            }
            invocationId++;
            await conn.SendAsync(target, argValues);
            Console.WriteLine($"Invoke method {target} with invocationId {invocationId}");
        }

        async Task ClientStream(Match match)
        {
            var target = match.Groups[1].Value;
            var args = match.Groups[2].Value;
            if (!StreamMethodDefineMap.TryGetValue(target, out var sig))
            {
                Console.WriteLine($"Undefined target: {target}");
                Console.WriteLine($"Please define the target first.");
                return;
            }
            var argStrings = args.Split(',').Select(x => x.Trim()).ToList();
            if (argStrings.Count != sig.Params.Length)
            {
                Console.WriteLine($"Invalid number of arguments for {target}: {argStrings.Count} != {sig.Params.Length}");
                return;
            }
            var argValues = new object[sig.Params.Length];
            var isValid = true;
            for (var i = 0; i < sig.Params.Length; i++)
            {
                try
                {
                    argValues[i] = sig.Params[i].Parse(argStrings[i]);
                }
                catch (Exception)
                {
                    Console.WriteLine($"Invalid argument type for {target}: {argValues[i]}");
                    isValid = false;
                    break;
                }
            }
            if (!isValid)
            {
                return;
            }
            var currentId = ++invocationId;
            var stream = await conn.StreamAsChannelCoreAsync(target, sig.Item.Type, argValues);
            Console.WriteLine($"Invoke stream method {target} with invocationId {currentId}");
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var item in stream.ReadAllAsync())
                    {
                        Console.WriteLine($"Stream item for {currentId}: {item}");
                    }
                    Console.WriteLine($"Stream {currentId} closed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Stream {currentId} error: {ex.Message}");
                }
            });
        }

        Task ClientListen(Match match)
        {
            var target = match.Groups[1].Value;
            var paramTypes = match.Groups[2].Value.Split(',').Select(x => x.Trim()).ToList();
            if (!CheckParameter(paramTypes))
            {
                return Task.CompletedTask;
            }
            _ = conn.On(
                target,
                [.. paramTypes.Select(x => TypeMap[x].Type)],
                args =>
                {
                    if (args.Length == 0)
                    {
                        Console.WriteLine($"Invoke method {target} with no args");
                    }
                    else
                    {
                        Console.WriteLine($"Invoke method {target} with args: {string.Join(", ", args)}");
                    }
                    return Task.CompletedTask;
                });
            return Task.CompletedTask;
        }
    }

    private static bool CheckParameter(List<string> argTypes)
    {
        if (argTypes.All(TypeMap.ContainsKey))
        {
            return true;
        }
        foreach (var p in argTypes)
        {
            if (TypeMap.ContainsKey(p))
            {
                continue;
            }
            Console.WriteLine($"Invalid argument types: {p}, supported argument types are: {string.Join(", ", TypeMap.Keys)}");
        }
        return false;
    }
}
