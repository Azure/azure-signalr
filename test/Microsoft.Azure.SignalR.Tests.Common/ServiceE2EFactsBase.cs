// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Testing.xunit;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class ServiceE2EFactsBase : VerifiableLoggedTest, IDisposable
    {
        private const int DefaultClientCount = 3;
        private const int DefaultDelayMilliseconds = 2000;
        private static readonly string _defaultMessage = $"Message from {nameof(ServiceE2EFactsBase)}";
        private readonly string _serverUrl;
        private readonly ITestServer _testServer;
        private readonly Func<string, int, ITestClientSet> _testClientSetFactory;
        private readonly IDisposable _verifiableLog;

        public ServiceE2EFactsBase(ITestServer testServer, Func<string, int, ITestClientSet> testClientSetFactory, ITestOutputHelper output) : base(output)
        {
            _testServer = testServer;
            _testClientSetFactory = testClientSetFactory;

            _verifiableLog = StartVerifiableLog(out var loggerFactory);
            _serverUrl = _testServer.StartAsync(loggerFactory).Result;
        }

        public void Dispose()
        {
            _testServer.StopAsync().Wait();
            _verifiableLog.Dispose();
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
            await clients.SendAsync(methodName, _defaultMessage);
            await Task.Delay(DefaultDelayMilliseconds);
            await clients.StopAsync();
            Assert.Equal(clients.Count, count);
        }

        // ...

        public static object[][] TestData =
        {
            new object[] { "Echo", DefaultClientCount, new Func<ITestClientSet, Task>(clients => clients.SendAsync("Echo",  sendCount: DefaultClientCount, messages: _defaultMessage))},
            new object[] { "Broadcast", DefaultClientCount, new Func<ITestClientSet, Task>(clients => clients.SendAsync("Broadcast", sendCount: DefaultClientCount, messages: _defaultMessage))},
            new object[] { "SendToGroup", DefaultClientCount, new Func<ITestClientSet, Task>(GroupTask)}
        };

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [MemberData(nameof(TestData))]
        public async Task RunE2ETests(string methodName, int expectedMessageCount, Func<ITestClientSet, Task> coreTask)
        {
            var clients = _testClientSetFactory(_serverUrl, DefaultClientCount);
            clients.AddListener(methodName, message => Interlocked.Increment(ref expectedMessageCount));
            await clients.StartAsync();
            await coreTask(clients);
            await Task.Delay(DefaultDelayMilliseconds);
            await clients.StopAsync();
            Assert.Equal(clients.Count, expectedMessageCount);
        }

        private static async Task GroupTask(ITestClientSet clients)
        {
            var connectionGroupMap = (from i in Enumerable.Range(0, DefaultClientCount)
                                      select new { ind = i, groupName = $"group_{i}" }).ToDictionary(t => t.ind, t => t.groupName);
            await clients.ManageGroupAsync("JoinGroup", connectionGroupMap);
            await Task.Delay(DefaultDelayMilliseconds);
            await clients.SendAsync("SendToGroup", _defaultMessage);
            await Task.Delay(DefaultDelayMilliseconds);
            await clients.ManageGroupAsync("LeaveGroup", connectionGroupMap);
            await Task.Delay(DefaultDelayMilliseconds);
            await clients.SendAsync("SendToGroup", _defaultMessage);
            await Task.Delay(DefaultDelayMilliseconds);
        }
    }
}