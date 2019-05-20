// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class ServiceE2EFactsBase : VerifiableLoggedTest, IDisposable
    {
        protected const int DefaultClientCount = 3;
        protected const int DefaultDelayMs = 2000;
        protected string _defaultMessage = $"Message from {nameof(ServiceE2EFactsBase)}";
        protected string _serverUrl;
        protected ITestServer _testServer;
        protected Func<string, int, ITestClientSet> _testClientSetFactory;
        protected ILoggerFactory _loggerFactory;

        public ServiceE2EFactsBase(ITestServer testServer, Func<string, int, ITestClientSet> testClientSetFactory, ITestOutputHelper output) : base(output)
        {
            _testServer = testServer;
            _testClientSetFactory = testClientSetFactory;

            StartVerifiableLog(out _loggerFactory);
            _serverUrl = _testServer.StartAsync(_loggerFactory).Result;
        }

        public async void Dispose()
        {
            await _testServer.StopAsync();
            _loggerFactory.Dispose();
        }

        [ConditionalFact]
        [SkipIfConnectionStringNotPresent]
        public async Task EchoTest()
        {
            var methodName = "Echo";
            var count = 0;
            var clients = _testClientSetFactory(_serverUrl, DefaultClientCount);
            clients.AddListener(methodName, message => Interlocked.Increment(ref count));
            await clients.StartAsync();
            await clients.AllSendAsync(methodName, _defaultMessage);
            await Task.Delay(DefaultDelayMs);
            await clients.StopAsync();
            Assert.Equal(clients.Count, count);
        }
    }
}