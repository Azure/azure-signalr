// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
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
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType);
            await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.All.SendAsync(MethodName, Message), ClientConnectionCount, receivedMessageDict);
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [InlineData(ServiceTransportType.Transient)]
        internal async Task SendToUserTest(ServiceTransportType serviceTransportType)
        {
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType);
            await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.User(_userNames[0]).SendAsync(MethodName, Message), 1, receivedMessageDict);
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [InlineData(ServiceTransportType.Transient)]
        internal async Task SendToUsersTest(ServiceTransportType serviceTransportType)
        {
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType);
            await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.Users(_userNames).SendAsync(MethodName, Message), ClientConnectionCount, receivedMessageDict);
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [InlineData(ServiceTransportType.Transient)]
        internal async Task SendToGroupTest(ServiceTransportType serviceTransportType)
        {
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType);
            Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Group(_groupNames[0]).SendAsync(MethodName, Message);
            await RunTestCore(clientEndpoint, clientAccessTokens, () => SendToGroupCore(serviceHubContext, sendTaskFunc), _userNames.Length / _groupNames.Length + _userNames.Length % _groupNames.Length, receivedMessageDict);
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [InlineData(ServiceTransportType.Transient)]
        internal async Task SendToGroupsTest(ServiceTransportType serviceTransportType)
        {
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType);
            Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Groups(_groupNames).SendAsync(MethodName, Message);
            await RunTestCore(clientEndpoint, clientAccessTokens, () => SendToGroupCore(serviceHubContext, sendTaskFunc), ClientConnectionCount, receivedMessageDict);
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

        private static async Task RunTestCore(string clientEndpoint, IEnumerable<string> clientAccessTokens, Func<Task> coreTask, int expectedReceivedMessageCount, ConcurrentDictionary<int, int> receivedMessageDict)
        {
            var connections = await CreateAndStartClientConnections(clientEndpoint, clientAccessTokens);
            ListenOnMessage(connections, receivedMessageDict);

            await coreTask();

            await Task.Delay(_timeout);

            var receivedMessageCount = (from pair in receivedMessageDict
                                        select pair.Value).Sum();
            Assert.Equal(expectedReceivedMessageCount, receivedMessageCount);
        }

        private static string[] GetTestStringList(string prefix, int count)
        {
            return (from i in Enumerable.Range(0, count)
                    select $"{prefix}{i}").ToArray();
        }

        private static async Task<(string ClientEndpoint, IEnumerable<string> ClientAccessTokens, IServiceHubContext ServiceHubContext)> InitAsync(ServiceTransportType serviceTransportType)
        {
            var serviceManager = GenerateServiceManager(TestConfiguration.Instance.ConnectionString, serviceTransportType);
            var serviceHubContext = await serviceManager.CreateHubContextAsync(HubName);

            var clientEndpoint = serviceManager.GetClientEndpoint(HubName);
            var clientAccessTokens = from userName in _userNames
                                     select serviceManager.GenerateClientAccessToken(HubName, userName);

            return (clientEndpoint, clientAccessTokens.ToArray(), serviceHubContext);
        }

        private static async Task<IList<HubConnection>> CreateAndStartClientConnections(string clientEndpoint, IEnumerable<string> clientAccessTokens)
        {
            var connections = (from clientAccessToken in clientAccessTokens
                               select CreateHubConnection(clientEndpoint, clientAccessToken)).ToList();

            await Task.WhenAll(from connection in connections
                               select connection.StartAsync());

            return connections;
        }

        private static void ListenOnMessage(IList<HubConnection> connections, ConcurrentDictionary<int, int> receivedMessageDict)
        {
            for (var i = 0; i < connections.Count(); i++)
            {
                var ind = i;
                connections[i].On(MethodName, (string message) =>
                {
                    if (message == Message)
                    {
                        receivedMessageDict.AddOrUpdate(ind, 1, (k, v) => v + 1);
                    }
                });
            }
        }

        private static IServiceManager GenerateServiceManager(string connectionString, ServiceTransportType serviceTransportType = ServiceTransportType.Transient)
        {
            var serviceManagerOptions = new ServiceManagerOptions
            {
                ConnectionString = connectionString,
                ServiceTransportType = serviceTransportType
            };

            return new ServiceManager(serviceManagerOptions);
        }

        private static HubConnection CreateHubConnection(string endpoint, string accessToken) =>
            new HubConnectionBuilder()
                .WithUrl(endpoint, option =>
                {
                    option.AccessTokenProvider = () =>
                    {
                        return Task.FromResult(accessToken);
                    };
                }).Build();
    }
}
