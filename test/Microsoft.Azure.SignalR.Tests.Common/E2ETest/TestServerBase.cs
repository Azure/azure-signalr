// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public abstract class TestServerBase : ITestServer
    {
        private static readonly int _maxRetry = 10;

        public async Task<string> StartAsync(ILoggerFactory loggerFactory)
        {
            var logger = loggerFactory.CreateLogger<TestServerBase>();

            for (int retry = 0; retry < _maxRetry; retry++)
            {
                try
                {
                    var serverUrl = GetRandomPortUrl();
                    await StartCoreAsync(serverUrl, loggerFactory);
                    logger.LogInformation($"Server started: {serverUrl}");
                    return serverUrl;
                }
                catch (IOException ex)
                {
                    if (ex.Message.Contains("address already in use") || ex.Message.Contains("Failed to bind to address"))
                    {
                        logger.LogWarning($"Retry: {retry + 1} times. Warning: {ex.Message}");
                        retry++;
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

        protected abstract Task StartCoreAsync(string serverUrl, ILoggerFactory loggerFactory);
    }
}