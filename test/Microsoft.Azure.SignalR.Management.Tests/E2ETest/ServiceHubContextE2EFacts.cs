// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.TestsCommon;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceHubContextE2EFacts
    {
        private const string HubName = "signalrBench";
        private const string MethodName = "SendMessage";
        private const string Message = "Hello client, have a nice day!";
        private const int ClientConnectionCount = 4;
        private const int GroupCount = 2;
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);
        private static readonly string[] _userNames = GetTestStringList("User", ClientConnectionCount);
        private static readonly string[] _groupNames = GetTestStringList("Group", GroupCount);

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [InlineData(ServiceTransportType.Transient)]
        internal async Task BroadcastTest(ServiceTransportType serviceTransportType)
        {
            (string clientEndpoint, IEnumerable<string> clientAccessTokens, IServiceHubContext serviceHubContext) = await InitAsync(serviceTransportType);
            await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.All.SendAsync(MethodName, Message), ClientConnectionCount);
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [InlineData(ServiceTransportType.Transient)]
        internal async Task SendToUserTest(ServiceTransportType serviceTransportType)
        {
            (string clientEndpoint, IEnumerable<string> clientAccessTokens, IServiceHubContext serviceHubContext) = await InitAsync(serviceTransportType);
            await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.User(_userNames[0]).SendAsync(MethodName, Message), 1);
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [InlineData(ServiceTransportType.Transient)]
        internal async Task SendToUsersTest(ServiceTransportType serviceTransportType)
        {
            (string clientEndpoint, IEnumerable<string> clientAccessTokens, IServiceHubContext serviceHubContext) = await InitAsync(serviceTransportType);
            await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.Users(_userNames).SendAsync(MethodName, Message), ClientConnectionCount);
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [InlineData(ServiceTransportType.Transient)]
        internal async Task SendToGroupTest(ServiceTransportType serviceTransportType)
        {
            (string clientEndpoint, IEnumerable<string> clientAccessTokens, IServiceHubContext serviceHubContext) = await InitAsync(serviceTransportType);
            Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Group(_groupNames[0]).SendAsync(MethodName, Message);
            await RunTestCore(clientEndpoint, clientAccessTokens, () => SendToGroupCore(serviceHubContext, sendTaskFunc), _userNames.Length / _groupNames.Length + _userNames.Length % _groupNames.Length);
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [InlineData(ServiceTransportType.Transient)]
        internal async Task SendToGroupsTest(ServiceTransportType serviceTransportType)
        {
            (string clientEndpoint, IEnumerable<string> clientAccessTokens, IServiceHubContext serviceHubContext) = await InitAsync(serviceTransportType);
            Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Groups(_groupNames).SendAsync(MethodName, Message);
            await RunTestCore(clientEndpoint, clientAccessTokens, () => SendToGroupCore(serviceHubContext, sendTaskFunc), ClientConnectionCount);
        }

        private static async Task SendToGroupCore(IServiceHubContext serviceHubContext, Func<Task> sendTask)
        {
            await Task.WhenAll(from i in Enumerable.Range(0, _userNames.Length)
                               select serviceHubContext.UserGroups.AddToGroupAsync(_userNames[i], _groupNames[i % _groupNames.Length]));
            await Task.Delay(_timeout);
            await sendTask();
            await Task.Delay(_timeout);
            await Task.WhenAll(from i in Enumerable.Range(0, _userNames.Length)
                               select serviceHubContext.UserGroups.RemoveFromGroupAsync(_userNames[i], _groupNames[i % _groupNames.Length]));
            await Task.Delay(_timeout);
            await sendTask();
            await Task.Delay(_timeout);
        }

        private static async Task RunTestCore(string clientEndpoint, IEnumerable<string> clientAccessTokens, Func<Task> taskFunc, int expectedReceivedMessageCount)
        {
            var connections = await CreateAndStartClientConnections(clientEndpoint, clientAccessTokens);
            var receivedMessageCount = new StrongBox<int>();
            ListenOnMessage(connections, () => Interlocked.Increment(ref receivedMessageCount.Value));

            Task task = null;
            try
            {
                await (task = taskFunc.Invoke());
            }
            finally
            {
                Assert.Null(task.Exception);
            }

            await WaitTaskComplete(expectedReceivedMessageCount, receivedMessageCount);

            Assert.Equal(expectedReceivedMessageCount, receivedMessageCount.Value);
        }

        private static async Task WaitTaskComplete(int expectedReceivedMessageCount, StrongBox<int> receivedMessageCount)
        {
            var length = 100;
            for (int i = 0; i < length; i++)
            {
                await Task.Delay((int)_timeout.TotalMilliseconds / length);
                if (expectedReceivedMessageCount == receivedMessageCount.Value)
                {
                    return;
                }
            }
        }

        private static string[] GetTestStringList(string prefix, int count)
        {
            return (from i in Enumerable.Range(0, count)
                    select $"{prefix}{i}").ToArray();
        }

        private static async Task<(string ClientEndpoint, IEnumerable<string> ClientAccessTokens, IServiceHubContext ServiceHubContext)> InitAsync(ServiceTransportType serviceTransportType)
        {
            var serviceManager = Utility.GenerateServiceManager(TestConfiguration.Instance.ConnectionString, serviceTransportType);
            var serviceHubContext = await serviceManager.CreateHubContextAsync(HubName);

            var clientEndpoint = serviceManager.GetClientEndpoint(HubName);
            var clientAccessTokens = from userName in _userNames
                                     select serviceManager.GenerateClientAccessToken(HubName, userName);

            return (clientEndpoint, clientAccessTokens.ToArray(), serviceHubContext);
        }

        private static async Task<IEnumerable<HubConnection>> CreateAndStartClientConnections(string clientEndpoint, IEnumerable<string> clientAccessTokens)
        {
            var connections = (from clientAccessToken in clientAccessTokens
                               select Utility.CreateHubConnection(clientEndpoint, clientAccessToken)).ToList();

            await Task.WhenAll(from connection in connections
                               select connection.StartAsync());

            return connections;
        }

        private static void ListenOnMessage(IEnumerable<HubConnection> connections, Action increaseReceivedMassageCount)
        {
            foreach(var connection in connections)
            {
                connection.On(MethodName, (string message) =>
                {
                    increaseReceivedMassageCount.Invoke();
                    Assert.Equal(Message, message);
                });
            }
        }
    }
}
