// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public abstract class TestServerBase : ITestServer
    {
        private static readonly int _maxRetry = 10;
        private ITestOutputHelper _output;

        public TestServerBase(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task<string> StartAsync()
        {
            for (int retry = 0; retry < _maxRetry; retry++)
            {
                try
                {
                    var serverUrl = GetRandomPortUrl();
                    await StartCoreAsync(serverUrl, _output);
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

            throw new IOException($"Fail to start server for {_maxRetry} times. Ports are already in used");
        }

        private static string GetRandomPortUrl()
        {
            return $"http://localhost:{StaticRandom.Next(49152, 65535)}";
        }

        public abstract Task StopAsync();

        protected abstract Task StartCoreAsync(string serverUrl, ITestOutputHelper output);
    }
}