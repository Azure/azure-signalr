// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public abstract class TestServerBase : ITestServer
    {
        private static readonly Random _rnd = new Random();
        private static readonly int _maxRetry = 10;
        private readonly ILogger _logger;

        public TestServerBase(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TestServerBase>();
        }

        public string Start()
        {
            for (int retry = 0; retry < _maxRetry; retry++)
            {
                try
                {
                    var serverUrl = GetRandomPortUrl();
                    StartCore(serverUrl);
                    return serverUrl;
                }
                catch (IOException ex)
                {
                    if (ex.Message.Contains("address already in use") || ex.Message.Contains("Failed to bind to address"))
                    {
                        _logger.LogWarning($"Retry: {retry + 1} times. Warning: {ex.Message}");
                        retry++;
                    }
                }
            }

            throw new IOException($"Fail to start server for {_maxRetry} times. Ports are already in used");
        }

        private static string GetRandomPortUrl()
        {
            return $"http://localhost:{_rnd.Next(49152, 65535)}";
        }

        public abstract void Stop();

        protected abstract void StartCore(string serverUrl);
    }
}