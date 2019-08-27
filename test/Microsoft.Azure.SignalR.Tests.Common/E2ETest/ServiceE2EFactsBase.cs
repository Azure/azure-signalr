// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Testing.xunit;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class ServiceE2EFactsBase : VerifiableLoggedTest
    {
        private readonly ITestServerFactory _testServerFactory;
        private readonly ITestClientSetFactory _testClientSetFactory;
        private readonly ITestOutputHelper _output;

        public static string DefaultMessage { get; } = $"Message from {nameof(ServiceE2EFactsBase)}";

        public static int DefaultClientCount { get; } = 3;

        public static int DefaultDelayMilliseconds { get; } = 3000;

        public static int DefaultSendGroupIndex { get; } = 0;

        public static object[][] TestDataBase { get; } = {
            new object[] { "Echo", DefaultClientCount, new Func<string, ITestClientSet, Task>((methodName, clients) => clients.SendAsync(methodName, sendCount : DefaultClientCount, messages : DefaultMessage)) },
            new object[] { "Broadcast", DefaultClientCount, new Func<string, ITestClientSet, Task>((methodName, clients) => clients.SendAsync(methodName, sendCount : 1, messages : DefaultMessage)) },
            new object[] { "SendToClient", DefaultClientCount, new Func<string, ITestClientSet, Task>((methodName, clients) => clients.SendAsync(methodName, sendCount : DefaultClientCount, messages : DefaultMessage)) },
            new object[] { "SendToUser", DefaultClientCount, new Func<string, ITestClientSet, Task>((methodName, clients) => clients.SendAsync(methodName, sendCount : DefaultClientCount, messages : DefaultMessage)) },
            new object[] { "SendToGroup", GetGroupSize(DefaultSendGroupIndex), new Func<string, ITestClientSet, Task>(GroupTask) },
        };

        private static IDictionary<int, string> ConnectionGroupMap
        {
            get
            {
                return (from i in Enumerable.Range(0, DefaultClientCount) select new { ind = i, groupName = GetGroupName(i) }).ToDictionary(t => t.ind, t => t.groupName);
            }
        }

        

        public ServiceE2EFactsBase(ITestServerFactory testServerFactory, ITestClientSetFactory testClientSetFactory, ITestOutputHelper output) : base(output)
        {
            _testServerFactory = testServerFactory;
            _testClientSetFactory = testClientSetFactory;
            _output = output;
        }

        protected async Task RunE2ETestsBase(string methodName, int expectedMessageCount, Func<string, ITestClientSet, Task> coreTask)
        {
            ITestServer server = null;
            try
            {
                server = _testServerFactory.Create(_output);
                var serverUrl = await server.StartAsync();
                var count = 0;
                var clients = _testClientSetFactory.Create(serverUrl, DefaultClientCount, _output);
                clients.AddListener(methodName, message => Interlocked.Increment(ref count));
                await clients.StartAsync();
                await coreTask(methodName, clients);
                await Task.Delay(DefaultDelayMilliseconds);
                await clients.StopAsync();
                await Task.Delay(DefaultDelayMilliseconds);
                Assert.Equal(expectedMessageCount, count);
            }
            finally
            {
                await server?.StopAsync();
            }

        }

        private static async Task GroupTask(string methodName, ITestClientSet clients)
        {
            await clients.ManageGroupAsync("JoinGroup", ConnectionGroupMap);
            await clients.SendAsync(methodName, sendCount: 1, messages: new[] { GetGroupName(DefaultSendGroupIndex), DefaultMessage });
            await Task.Delay(DefaultDelayMilliseconds);
            await clients.ManageGroupAsync("LeaveGroup", ConnectionGroupMap);
            await clients.SendAsync(methodName, messages: new[] { GetGroupName(DefaultSendGroupIndex), DefaultMessage });
            await Task.Delay(DefaultDelayMilliseconds);
        }

        private static string GetGroupName(int ind) => $"group_{ind % 2}";

        protected static int GetGroupSize(int ind) => (from entry in ConnectionGroupMap where GetGroupName(ind) == entry.Value select entry).Count();

        private void Shuffle<T>(T[] array)
        {
            for (var i = array.Length - 1; i > 0; i--)
            {
                int k = StaticRandom.Next(i + 1);
                T value = array[k];
                array[k] = array[i];
                array[i] = value;
            }
        }
    }
}