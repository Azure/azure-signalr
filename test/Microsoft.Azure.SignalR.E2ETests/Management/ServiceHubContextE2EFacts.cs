// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceHubContextE2EFacts : VerifiableLoggedTest
    {
        private const string HubName = "ManagemnetTestHub";
        private const string MethodName = "SendMessage";
        private const string Message = "Hello client, have a nice day!";
        private const int ClientConnectionCount = 4;
        private const int GroupCount = 2;
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);
        private static readonly string[] _userNames = GetTestStringList("User", ClientConnectionCount);
        private static readonly string[] _groupNames = GetTestStringList("Group", GroupCount);
        private static readonly ServiceTransportType[] _serviceTransportType = new ServiceTransportType[] { ServiceTransportType.Transient, ServiceTransportType.Persistent };
        private static readonly string[] _appNames = new string[] { "appName", "", null };
        private readonly ITestServerFactory _testServerFactory;

        public ServiceHubContextE2EFacts(ITestOutputHelper output) : base(output)
        {
            _testServerFactory = new TestServerFactory();
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
                var userGroupDict = GenerateUserGroupDict(_userNames, _groupNames);
                await RunTestCore(clientEndpoint, clientAccessTokens,
                    () => SendToGroupCore(serviceHubContext, userGroupDict, sendTaskFunc, AddUserToGroupAsync, UserRemoveFromGroupsOneByOneAsync),
                    _userNames.Length / _groupNames.Length + _userNames.Length % _groupNames.Length, receivedMessageDict);
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
                var userGroupDict = GenerateUserGroupDict(_userNames, _groupNames);
                await RunTestCore(clientEndpoint, clientAccessTokens,
                    () => SendToGroupCore(serviceHubContext, userGroupDict, sendTaskFunc, AddUserToGroupAsync, UserRemoveFromGroupsOneByOneAsync),
                    ClientConnectionCount, receivedMessageDict);
            }
            finally
            {
                await serviceHubContext.DisposeAsync();
            }
        }

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [MemberData(nameof(TestData))]
        internal async Task RemoveUserFromAllGroupsTest(ServiceTransportType serviceTransportType, string appName)
        {
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName);
            try
            {
                Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Groups(_groupNames).SendAsync(MethodName, Message);
                var userGroupDict = new Dictionary<string, List<string>> { { _userNames[0], _groupNames.ToList() } };
                await RunTestCore(clientEndpoint, clientAccessTokens,
                    () => SendToGroupCore(serviceHubContext, userGroupDict, sendTaskFunc, AddUserToGroupAsync, UserRemoveFromAllGroupsAsync),
                    _groupNames.Length, receivedMessageDict);
            }
            finally
            {
                await serviceHubContext.DisposeAsync();
            }
        }

        [Theory(Skip = "Not Ready")]
        [MemberData(nameof(TestData))]
        internal async Task SendToConnectionTest(ServiceTransportType serviceTransportType, string appName)
        {
            var testServer = _testServerFactory.Create(TestOutputHelper);
            await testServer.StartAsync(new Dictionary<string, string> { [TestStartup.ApplicationName] = appName });

            var task = testServer.HubConnectionManager.WaitForConnectionCountAsync(1);

            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName);
            try
            {
                await RunTestCore(clientEndpoint, clientAccessTokens,
                    async () =>
                    {
                        var connectionId = await SignalR.Tests.Common.TaskExtensions.OrTimeout(task);
                        await serviceHubContext.Clients.Client(connectionId).SendAsync(MethodName, Message);
                    },
                    1, receivedMessageDict);
            }
            finally
            {
                await serviceHubContext.DisposeAsync();
            }
        }

        [Theory(Skip = "Not Ready")]
        [SkipIfConnectionStringNotPresent]
        [MemberData(nameof(TestData))]
        internal async Task ConnectionJoinLeaveGroupTest(ServiceTransportType serviceTransportType, string appName)
        {
            var testServer = _testServerFactory.Create(TestOutputHelper);
            await testServer.StartAsync(new Dictionary<string, string> { [TestStartup.ApplicationName] = appName });

            var task = testServer.HubConnectionManager.WaitForConnectionCountAsync(1);

            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName);
            try
            {
                await RunTestCore(clientEndpoint, clientAccessTokens,
                    async () =>
                    {
                        var connectionId = await SignalR.Tests.Common.TaskExtensions.OrTimeout(task);
                        await serviceHubContext.Groups.AddToGroupAsync(connectionId, _groupNames[0]);
                        await serviceHubContext.Clients.Group(_groupNames[0]).SendAsync(MethodName, Message);
                        // We can't guarantee the order between the send group and the following leave group
                        await Task.Delay(_timeout);
                        await serviceHubContext.Groups.RemoveFromGroupAsync(connectionId, _groupNames[0]);
                        await serviceHubContext.Clients.Group(_groupNames[0]).SendAsync(MethodName, Message);
                    },
                    1, receivedMessageDict);
            }
            finally
            {
                await serviceHubContext.DisposeAsync();
            }
        }

        [ConditionalFact]
        [SkipIfConnectionStringNotPresent]
        internal async Task StopServiceHubContextTest()
        {
            using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
            {
                var serviceManager = new ServiceManagerBuilder()
                    .WithOptions(o =>
                    {
                        o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                        o.ConnectionCount = 1;
                        o.ServiceTransportType = ServiceTransportType.Persistent;
                    })
                    .Build();
                var serviceHubContext = await serviceManager.CreateHubContextAsync("hub", loggerFactory);
                await ((ServiceHubContext)serviceHubContext).StopConnectionAsync();
                await Task.Delay(100);
                var status = ((ServiceHubContext) serviceHubContext).GetConnectionStatus();
                await ((ServiceHubContext) serviceHubContext).DisposeAsync();
                Assert.Equal(ServiceConnectionStatus.Disconnected, status);
            }
        }

        private static IDictionary<string, List<string>> GenerateUserGroupDict(IList<string> userNames, IList<string> groupNames)
        {
            return (from i in Enumerable.Range(0, userNames.Count)
                    select (User: userNames[i], Group: groupNames[i % groupNames.Count]))
                    .ToDictionary(t => t.User, t => new List<string> { t.Group });
        }

        private static Task AddUserToGroupAsync(IServiceHubContext serviceHubContext, IDictionary<string, List<string>> userGroupDict)
        {
            return Task.WhenAll(from usergroup in userGroupDict
                                select Task.WhenAll(from grp in usergroup.Value
                                                    select serviceHubContext.UserGroups.AddToGroupAsync(usergroup.Key, grp)));
        }

        private static Task UserRemoveFromGroupsOneByOneAsync(IServiceHubContext serviceHubContext, IDictionary<string, List<string>> userGroupDict)
        {
            return Task.WhenAll(from usergroup in userGroupDict
                                select Task.WhenAll(from grp in usergroup.Value
                                                    select serviceHubContext.UserGroups.RemoveFromGroupAsync(usergroup.Key, grp)));
        }

        private static Task UserRemoveFromAllGroupsAsync(IServiceHubContext serviceHubContext, IDictionary<string, List<string>> userGroupDict)
        {
            return Task.WhenAll(from user in userGroupDict.Keys
                                select serviceHubContext.UserGroups.RemoveFromAllGroupsAsync(user));
        }

        private static async Task SendToGroupCore(
            IServiceHubContext serviceHubContext,
            IDictionary<string, List<string>> userGroupDict,
            Func<Task> sendTask, Func<IServiceHubContext, IDictionary<string, List<string>>, Task> userAddToGroupTask,
            Func<IServiceHubContext, IDictionary<string, List<string>>, Task> userRemoveFromGroupTask)
        {
            await userAddToGroupTask(serviceHubContext, userGroupDict);
            await Task.Delay(_timeout);
            await sendTask();
            await Task.Delay(_timeout);
            await userRemoveFromGroupTask(serviceHubContext, userGroupDict);
            await Task.Delay(_timeout);
            await sendTask();
            await Task.Delay(_timeout);
        }

        private static async Task RunTestCore(string clientEndpoint, IEnumerable<string> clientAccessTokens, Func<Task> coreTask, int expectedReceivedMessageCount, ConcurrentDictionary<int, int> receivedMessageDict)
        {
            IList<HubConnection> connections = null;
            CancellationTokenSource cancellationTokenSource = null;
            try
            {
                connections = await CreateAndStartClientConnections(clientEndpoint, clientAccessTokens);
                cancellationTokenSource = new CancellationTokenSource();
                HandleHubConnection(connections, cancellationTokenSource);
                ListenOnMessage(connections, receivedMessageDict);

                Assert.False(cancellationTokenSource.Token.IsCancellationRequested);

                await coreTask();
                await Task.Delay(_timeout);

                Assert.False(cancellationTokenSource.Token.IsCancellationRequested);

                var receivedMessageCount = (from pair in receivedMessageDict
                                            select pair.Value).Sum();
                Assert.Equal(expectedReceivedMessageCount, receivedMessageCount);
            }
            finally
            {
                cancellationTokenSource?.Dispose();
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
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(opt =>
                {
                    opt.ConnectionString = connectionString;
                    opt.ServiceTransportType = serviceTransportType;
                    opt.ApplicationName = appName;
                })
                .WithCallingAssembly()
                .Build();
            return serviceManager;
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

        private static void HandleHubConnection(IList<HubConnection> connections, CancellationTokenSource cancellationTokenSource)
        {
            foreach (var connection in connections)
            {
                connection.Closed += ex =>
                {
                    cancellationTokenSource.Cancel();
                    return Task.CompletedTask;
                };
            }
        }
    }
}
