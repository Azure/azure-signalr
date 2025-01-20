// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests.Common.E2ETest;

public abstract class TestServerBase(ITestOutputHelper output) : ITestServer
{
    private static readonly int MaxRetry = 10;

    private readonly ITestOutputHelper _output = output;

    public abstract TestHubConnectionManager HubConnectionManager { get; }

    public async Task<string> StartAsync(Dictionary<string, string> configuration = null)
    {
        for (var retry = 0; retry < MaxRetry; retry++)
        {
            try
            {
                var serverUrl = GetRandomPortUrl();
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

    public abstract Task StopAsync();

    protected abstract Task StartCoreAsync(string serverUrl, ITestOutputHelper output, Dictionary<string, string> configuration);

    private static string GetRandomPortUrl()
    {
        return $"http://localhost:{StaticRandom.Next(49152, 65535)}";
    }
}