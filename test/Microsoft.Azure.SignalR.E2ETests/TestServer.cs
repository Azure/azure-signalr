// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.E2ETests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.E2ETests;

#nullable enable

internal sealed class TestServer(ITestOutputHelper output) : ITestServer, IDisposable
{
    private const int MaxRetry = 10;

    private static int CurrentPortOffset = 11000;

    private readonly ITestOutputHelper _output = output;

    private IWebHost? _host;

    public bool EnableStatefulReconnect { get; init; }

    public string? ConnectionString { get; init; }

    public async Task<string> StartAsync(Dictionary<string, string?>? configuration = null)
    {
        for (var retry = 0; retry < MaxRetry; retry++)
        {
            try
            {
                var serverUrl = GetUrl();
                await StartCoreAsync(serverUrl, _output, configuration);
                _output.WriteLine($"Server started: {serverUrl}");
                return serverUrl;
            }
            catch (IOException ex)
            {
                if (ex.Message.Contains("address already in use") ||
                    ex.Message.Contains("Failed to bind to address"))
                {
                    _output.WriteLine($"Retry: {retry + 1} times. Warning: {ex.Message}");
                }
                else
                {
                    throw;
                }
            }
        }

        throw new IOException($"Fail to start server for {MaxRetry} times. Ports are already in used");
    }

    public void Dispose() => _host?.Dispose();

    public async Task StopAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
        }
    }

    private static int GetPortOffset() => Interlocked.Increment(ref CurrentPortOffset);

    private static string GetUrl()
    {
        return $"http://localhost:{GetPortOffset()}";
    }

    private Task StartCoreAsync(string serverUrl, ITestOutputHelper output, Dictionary<string, string?>? configuration)
    {
        configuration ??= [];
        if (ConnectionString != null)
        {
            configuration[TestStartup.ConnectionString] = ConnectionString;
        }
        _host = new WebHostBuilder()
            .ConfigureLogging(logging => logging.AddXunit(output))
            .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
            .UseStartup<TestStartup>()
            .UseUrls(serverUrl)
            .UseKestrel()
            .Build();
        return _host.StartAsync();
    }
}
