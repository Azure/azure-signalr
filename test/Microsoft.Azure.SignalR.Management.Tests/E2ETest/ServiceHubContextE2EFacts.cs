// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceHubContextE2EFacts : VerifiableLoggedTest
    {
        private const string HubName = "signalrBench";
        private const string MethodName = "SendMessage";
        private const string Message = "Hello client, have a nice day!";
        private const int ClientConnectionCount = 4;
        private const int GroupCount = 2;
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);
        private static readonly string[] _userNames = GetTestStringList("User", ClientConnectionCount);
        private static readonly string[] _groupNames = GetTestStringList("Group", GroupCount);
        private static readonly ServiceTransportType[] _serviceTransportType = new ServiceTransportType[] { ServiceTransportType.Transient, ServiceTransportType.Persistent };
        private static readonly string[] _appNames = new string[] { "appName", "", null };

        public ServiceHubContextE2EFacts(ITestOutputHelper output) : base(output)
        {
        }

        public static IEnumerable<object[]> TestData => from serviceTransportType in _serviceTransportType
                                                        from appName in _appNames
                                                        select new object[] { serviceTransportType, appName };

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [MemberData(nameof(TestData))]
        internal async Task BroadcastTest(ServiceTransportType serviceTransportType, string appName)
        {
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName);
            try
            {
                await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.All.SendAsync(MethodName, Message), ClientConnectionCount, receivedMessageDict);
            }
            finally
            {
                await serviceHubContext.DisposeAsync();
            }
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [MemberData(nameof(TestData))]
        internal async Task SendToUserTest(ServiceTransportType serviceTransportType, string appName)
        {
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName);
            try
            {
                await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.User(_userNames[0]).SendAsync(MethodName, Message), 1, receivedMessageDict);
            }
            finally
            {
                await serviceHubContext.DisposeAsync();
            }
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [MemberData(nameof(TestData))]
        internal async Task SendToUsersTest(ServiceTransportType serviceTransportType, string appName)
        {
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName);
            try
            {
                await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.Users(_userNames).SendAsync(MethodName, Message), ClientConnectionCount, receivedMessageDict);
            }
            finally
            {
                await serviceHubContext.DisposeAsync();
            }
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [MemberData(nameof(TestData))]
        internal async Task SendToGroupTest(ServiceTransportType serviceTransportType, string appName)
        {
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName);
            try
            {
                Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Group(_groupNames[0]).SendAsync(MethodName, Message);
                await RunTestCore(clientEndpoint, clientAccessTokens, () => SendToGroupCore(serviceHubContext, sendTaskFunc), _userNames.Length / _groupNames.Length + _userNames.Length % _groupNames.Length, receivedMessageDict);
            }
            finally
            {
                await serviceHubContext.DisposeAsync();
            }
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [MemberData(nameof(TestData))]
        internal async Task SendToGroupsTest(ServiceTransportType serviceTransportType, string appName)
        {
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName);
            try
            {
                Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Groups(_groupNames).SendAsync(MethodName, Message);
                await RunTestCore(clientEndpoint, clientAccessTokens, () => SendToGroupCore(serviceHubContext, sendTaskFunc), ClientConnectionCount, receivedMessageDict);
            }
            finally
            {
                await serviceHubContext.DisposeAsync();
            }
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
            IList<HubConnection> connections = null;
            try
            {
                StrongBox<bool> closed = new StrongBox<bool>(false);
                connections = await CreateAndStartClientConnections(clientEndpoint, clientAccessTokens);
                HandleHubConnection(connections, closed);
                ListenOnMessage(connections, receivedMessageDict);

                await Task.Delay(_timeout);
                Assert.False(closed.Value);

                await coreTask();
                await Task.Delay(_timeout);

                var receivedMessageCount = (from pair in receivedMessageDict
                                            select pair.Value).Sum();
                Assert.Equal(expectedReceivedMessageCount, receivedMessageCount);
            }
            finally
            {
                if (connections != null)
                {
                    await Task.WhenAll(from connection in connections
                                       select connection.StopAsync());
                }
            }
        }

        private static string[] GetTestStringList(string prefix, int count)
        {
            return (from i in Enumerable.Range(0, count)
                    select $"{prefix}{i}").ToArray();
        }

        private async Task<(string ClientEndpoint, IEnumerable<string> ClientAccessTokens, IServiceHubContext ServiceHubContext)> InitAsync(ServiceTransportType serviceTransportType, string appName)
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {

                var serviceManager = GenerateServiceManager(TestConfiguration.Instance.ConnectionString, serviceTransportType, appName);
                var serviceHubContext = await serviceManager.CreateHubContextAsync(HubName, loggerFactory);

                var clientEndpoint = serviceManager.GetClientEndpoint(HubName);
                var clientAccessTokens = from userName in _userNames
                                         select serviceManager.GenerateClientAccessToken(HubName, userName);

                return (clientEndpoint, clientAccessTokens.ToArray(), serviceHubContext);
            }
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

        private static IServiceManager GenerateServiceManager(string connectionString, ServiceTransportType serviceTransportType = ServiceTransportType.Transient, string appName = null)
        {
            var serviceManagerOptions = new ServiceManagerOptions
            {
                ConnectionString = connectionString,
                ServiceTransportType = serviceTransportType,
                ApplicationName = appName
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

        private static void HandleHubConnection(IList<HubConnection> connections, StrongBox<bool> closed)
        {
            foreach (var connection in connections)
            {
                connection.Closed += ex =>
                {
                    closed.Value = true;
                    return Task.CompletedTask;
                };
            }
        }
    }
}
