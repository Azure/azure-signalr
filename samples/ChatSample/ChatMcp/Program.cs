// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
using System;

using ChatMcp;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var url = Environment.GetEnvironmentVariable("ServerUrl");
if (string.IsNullOrEmpty(url))
{
    url = "http://localhost:5050/chat";
}
if (!Uri.TryCreate(url, UriKind.Absolute, out var serverUrl))
{
    Console.WriteLine($"Invalid ServerUrl: {url}");
    return;
}

await using var connection = new HubConnectionBuilder()
    .WithUrl(url)
    .WithAutomaticReconnect()
    .Build();
await connection.StartAsync();

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddConsole(consoleLogOptions =>
{
    // Configure all logs to go to stderr
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddSingleton(new ChatProxy(connection))
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
await builder.Build().RunAsync();
