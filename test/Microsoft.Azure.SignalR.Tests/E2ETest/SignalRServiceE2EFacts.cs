// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing.xunit;
#if NET461
using Microsoft.Azure.SignalR.AspNet.Tests;
#else
using Microsoft.Azure.SignalR.Tests;
#endif
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.E2ETests
{
    public class SignalRServiceE2EFacts : VerifiableLoggedTest
    {
        private const int ClientCount = 3;
        private const int _defaultDelayMs = 2000;
        private static readonly string DefaultMessage = $"Message from {nameof(SignalRServiceE2EFacts)}";
        private ITestServer _server;
        private ILoggerFactory _loggerFactory;
        private string _serverUrl;

        public SignalRServiceE2EFacts(ITestOutputHelper output) : base(output)
        {
            StartVerifiableLog(out _loggerFactory, LogLevel.Debug);
            _server = new TestServer(_loggerFactory);
            _serverUrl = _server.Start();
        }

        ~SignalRServiceE2EFacts()
        {
            _server.Stop();
            _loggerFactory.Dispose();
        }

        [ConditionalFact]
        [SkipIfConnectionStringNotPresent]
        public async Task EchoTest()
        {
            var methodName = "Echo";
            var count = 0;
            var clients = new TestClientSet().Create(_serverUrl, ClientCount);
            clients.AddListener(methodName, message => Interlocked.Increment(ref count));
            await clients.StartAsync();
            await clients.AllSendAsync(methodName, DefaultMessage);
            await Task.Delay(_defaultDelayMs);
            await clients.StopAsync();
            Assert.Equal(clients.Count, count);
        }
    }
}