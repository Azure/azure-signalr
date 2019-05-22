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
        public static object[][] TestData =
        {
            new object[] { "Echo", DefaultClientCount, new Func<string, ITestClientSet, Task>((methodName, clients) => clients.SendAsync(methodName,  sendCount: DefaultClientCount, messages: _defaultMessage))},
            //new object[] { "Broadcast", DefaultClientCount, new Func<string, ITestClientSet, Task>((methodName, clients) => clients.SendAsync(methodName, sendCount: 1, messages: _defaultMessage))},
            //new object[] {"SendToClient", DefaultClientCount, new Func<string, ITestClientSet, Task>((methodName, clients) => clients.SendAsync(methodName, sendCount: DefaultClientCount, messages: _defaultMessage))}, 
            //new object[] { "SendToGroup", GetGroupSize(DefaultSendGroupIndex), new Func<string, ITestClientSet, Task>(GroupTask)}
        };

        private const int DefaultClientCount = 3;
        private const int DefaultDelayMilliseconds = 2000;
        private const int DefaultSendGroupIndex = 0;

        private static readonly string _defaultMessage = $"Message from {nameof(ServiceE2EFactsBase)}";
        private static IDictionary<int, string> _connectionGroupMap = (from i in Enumerable.Range(0, DefaultClientCount)
            select new { ind = i, groupName = GetGroupName(i) }).ToDictionary(t => t.ind, t => t.groupName);

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

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [MemberData(nameof(TestData))]
        public async Task RunE2ETests(string methodName, int expectedMessageCount, Func<string, ITestClientSet, Task> coreTask)
        {
            var clients = _testClientSetFactory(_serverUrl, DefaultClientCount);
            clients.AddListener(methodName, message => Interlocked.Increment(ref expectedMessageCount));
            await clients.StartAsync();
            await coreTask(methodName, clients);
            await Task.Delay(DefaultDelayMilliseconds);
            await clients.StopAsync();
            Assert.Equal(clients.Count, expectedMessageCount);
        }

        private static async Task GroupTask(string methodName, ITestClientSet clients)
        {
            await clients.ManageGroupAsync("JoinGroup", _connectionGroupMap);
            await Task.Delay(DefaultDelayMilliseconds);
            await clients.SendAsync(methodName, messages: new[] { GetGroupName(DefaultSendGroupIndex), _defaultMessage });
            await Task.Delay(DefaultDelayMilliseconds);
            await clients.ManageGroupAsync("LeaveGroup", _connectionGroupMap);
            await Task.Delay(DefaultDelayMilliseconds);
            await clients.SendAsync(methodName, messages: new[] { GetGroupName(DefaultSendGroupIndex), _defaultMessage });
            await Task.Delay(DefaultDelayMilliseconds);
        }

        public static string GetGroupName(int ind) => $"group_{ind % 2}";

        public static int GetGroupSize(int ind) => (from entry in _connectionGroupMap
                                                    where GetGroupName(ind) == entry.Value
                                                    select entry).Count();
    }
}