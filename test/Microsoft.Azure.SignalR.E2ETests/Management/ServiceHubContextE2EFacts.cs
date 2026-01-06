// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using MessagePack;
using MessagePack.Formatters;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Microsoft.Azure.SignalR.Management.Tests;

public class ServiceHubContextE2EFacts : VerifiableLoggedTest
{
    private const string HubName = "ManagemnetTestHub";
    private const string MethodName = "SendMessage";
    private const string Message = "Hello client, have a nice day!";
    private const int ClientConnectionCount = 4;
    private const int GroupCount = 2;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private static readonly ServiceTransportType[] ServiceTransportType = [Management.ServiceTransportType.Transient, Management.ServiceTransportType.Persistent];
    private static readonly string[] AppNames = ["appName", "", null];
    private readonly ITestServerFactory _testServerFactory;

    public ServiceHubContextE2EFacts(ITestOutputHelper output) : base(output)
    {
        _testServerFactory = new TestServerFactory();
    }

    public static IEnumerable<object[]> TestData => from serviceTransportType in ServiceTransportType
                                                    from appName in AppNames
                                                    select new object[] { serviceTransportType, appName };

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TestData))]
    internal async Task BroadcastTest(ServiceTransportType serviceTransportType, string appName)
    {
        var userNames = GenerateRandomNames(ClientConnectionCount);
        var receivedMessageDict = new ConcurrentDictionary<int, int>();
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
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
    internal async Task BroadcastExceptTest(ServiceTransportType serviceTransportType, string appName)
    {
        var method = nameof(BroadcastExceptTest);
        var msg = Guid.NewGuid().ToString();
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var logger = loggerFactory.CreateLogger<ServiceHubContextE2EFacts>();
            var serviceManager = GenerateServiceManager(TestConfiguration.Instance.ConnectionString, serviceTransportType, appName);
            var hubContext = await serviceManager.CreateHubContextAsync(HubName) as ServiceHubContextImpl;
            var connectionCount = 3;
            var tcsDict = new ConcurrentDictionary<string, TaskCompletionSource>();
            TestOutputHelper.WriteLine($"Message is {msg}");
            var connections = await Task.WhenAll(Enumerable.Range(0, connectionCount).Select(async _ =>
             {
                 var negotiationResponse = await hubContext.NegotiateAsync(null, default);
                 var connection = CreateHubConnection(negotiationResponse.Url, negotiationResponse.AccessToken);
                 await connection.StartAsync();
                 var src = new TaskCompletionSource();
                 tcsDict.TryAdd(connection.ConnectionId, src);
                 connection.On(method, (string receivedMsg) =>
                 {
                     TestOutputHelper.WriteLine($"Connection {connection.ConnectionId} received msg : {receivedMsg}");
                     if (receivedMsg == msg)
                     {
                         src.SetResult();
                     }
                 });
                 return connection;
             }));
            var excluded = connections.First().ConnectionId;
            await hubContext.Clients.AllExcept(new string[] { excluded }).SendAsync(method, msg);
            await Task.WhenAll(tcsDict.Where(item => item.Key != excluded).Select(i => i.Value.Task)).OrTimeout(); // await included connections to receive msg
            Assert.False(tcsDict[excluded].Task.IsCompleted);

            //clean
            await Task.WhenAll(connections.Select(async conn => await conn.DisposeAsync()));
            await hubContext.DisposeAsync();
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TestData))]
    internal async Task SendToUserTest(ServiceTransportType serviceTransportType, string appName)
    {
        var userNames = GenerateRandomNames(ClientConnectionCount);
        var receivedMessageDict = new ConcurrentDictionary<int, int>();
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
        try
        {
            await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.User(userNames[0]).SendAsync(MethodName, Message), 1, receivedMessageDict);
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
        var userNames = GenerateRandomNames(ClientConnectionCount);
        var receivedMessageDict = new ConcurrentDictionary<int, int>();
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
        try
        {
            await RunTestCore(clientEndpoint, clientAccessTokens, () => serviceHubContext.Clients.Users(userNames).SendAsync(MethodName, Message), ClientConnectionCount, receivedMessageDict);
        }
        finally
        {
            await serviceHubContext.DisposeAsync();
        }
    }

    // keep the same behavior with https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/server/Core/src/DefaultHubLifetimeManager.cs
    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TestData))]
    internal async Task SendToEmptyReceiversTest(ServiceTransportType serviceTransportType, string appName)
    {
        var userNames = GenerateRandomNames(ClientConnectionCount);
        var receivedMessageDict = new ConcurrentDictionary<int, int>();
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
        var emptyTargets = new List<string>();
        try
        {
            // expect no exceptions
            var exception = await Record.ExceptionAsync(async () =>
            {
                await serviceHubContext.Clients.Users(emptyTargets).SendAsync(MethodName, Message);
                await serviceHubContext.Clients.Clients(emptyTargets).SendAsync(MethodName, Message);
                await serviceHubContext.Clients.Groups(emptyTargets).SendAsync(MethodName, Message);
            });
            Assert.Null(exception);
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
        var userNames = GenerateRandomNames(ClientConnectionCount);
        var groupNames = GenerateRandomNames(GroupCount);
        var receivedMessageDict = new ConcurrentDictionary<int, int>();
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
        try
        {
            Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Group(groupNames[0]).SendAsync(MethodName, Message);
            var userGroupDict = GenerateUserGroupDict(userNames, groupNames);
            await RunTestCore(clientEndpoint, clientAccessTokens,
                () => SendToGroupCore(serviceHubContext, userGroupDict, sendTaskFunc, AddUserToGroupAsync, UserRemoveFromGroupsOneByOneAsync),
                userNames.Length / groupNames.Length + userNames.Length % groupNames.Length, receivedMessageDict);
        }
        finally
        {
            await serviceHubContext.DisposeAsync();
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TestData))]
    internal async Task SendToGroupExceptTest(ServiceTransportType serviceTransportType, string appName)
    {
        var method = nameof(SendToGroupExceptTest);
        var msg = Guid.NewGuid().ToString();
        var group = nameof(SendToGroupExceptTest);
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var logger = loggerFactory.CreateLogger<ServiceHubContextE2EFacts>();
            var serviceManager = GenerateServiceManager(TestConfiguration.Instance.ConnectionString, serviceTransportType, appName);
            var hubContext = await serviceManager.CreateHubContextAsync(HubName) as ServiceHubContextImpl;
            var connectionCount = 3;
            var tcsDict = new ConcurrentDictionary<string, TaskCompletionSource>();
            TestOutputHelper.WriteLine($"Message is {msg}");
            var connections = await Task.WhenAll(Enumerable.Range(0, connectionCount).Select(async _ =>
            {
                var negotiationResponse = await hubContext.NegotiateAsync(null, default);
                var connection = CreateHubConnection(negotiationResponse.Url, negotiationResponse.AccessToken);
                await connection.StartAsync();
                var src = new TaskCompletionSource();
                tcsDict.TryAdd(connection.ConnectionId, src);
                connection.On(method, (string receivedMsg) =>
                {
                    TestOutputHelper.WriteLine($"Connection {connection.ConnectionId} received msg : {receivedMsg}");
                    if (receivedMsg == msg)
                    {
                        src.SetResult();
                    }
                });
                await hubContext.Groups.AddToGroupAsync(connection.ConnectionId, group);
                return connection;
            }));
            var excluded = connections.First().ConnectionId;
            await hubContext.Clients.GroupExcept(group, excluded).SendAsync(method, msg);

            // await included connections to receive msg
            await Task.WhenAll(tcsDict.Where(item => item.Key != excluded).Select(i => i.Value.Task)).OrTimeout();
            Assert.False(tcsDict[excluded].Task.IsCompleted);

            //clean
            await Task.WhenAll(connections.Select(async conn => await conn.DisposeAsync()));
            await hubContext.DisposeAsync();
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TestData))]
    internal async Task TestAddUserToGroupWithTtl(ServiceTransportType serviceTransportType, string appName)
    {
        var userNames = GenerateRandomNames(ClientConnectionCount);
        var groupNames = GenerateRandomNames(GroupCount);
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
        try
        {
            var userGroupDict = GenerateUserGroupDict(userNames, groupNames);
            var receivedMessageDict = new ConcurrentDictionary<int, int>();
            await RunTestCore(
                clientEndpoint,
                clientAccessTokens,
                () => SendToGroupCore(serviceHubContext, userGroupDict, SendAsync, (c, d) => AddUserToGroupWithTtlAsync(c, d, TimeSpan.FromSeconds(10)), Empty),
                (userNames.Length / groupNames.Length + userNames.Length % groupNames.Length) * 2,
                receivedMessageDict);

            await Task.Delay(TimeSpan.FromSeconds(30));
            receivedMessageDict.Clear();
            await RunTestCore(
                clientEndpoint,
                clientAccessTokens,
                () => SendToGroupCore(serviceHubContext, userGroupDict, SendAsync, Empty, Empty),
                0,
                receivedMessageDict);
        }
        finally
        {
            await serviceHubContext.DisposeAsync();
        }

        Task SendAsync() =>
            serviceHubContext.Clients.Group(groupNames[0]).SendAsync(MethodName, Message);

        static Task Empty(IServiceHubContext context, IDictionary<string, List<string>> dict) =>
            Task.CompletedTask;
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TestData))]
    internal async Task SendToGroupsTest(ServiceTransportType serviceTransportType, string appName)
    {
        var userNames = GenerateRandomNames(ClientConnectionCount);
        var groupNames = GenerateRandomNames(GroupCount);
        var receivedMessageDict = new ConcurrentDictionary<int, int>();
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
        try
        {
            Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Groups(groupNames).SendAsync(MethodName, Message);
            var userGroupDict = GenerateUserGroupDict(userNames, groupNames);
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
        var userNames = GenerateRandomNames(ClientConnectionCount);
        var groupNames = GenerateRandomNames(GroupCount);
        var receivedMessageDict = new ConcurrentDictionary<int, int>();
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
        try
        {
            Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Groups(groupNames).SendAsync(MethodName, Message);
            var userGroupDict = new Dictionary<string, List<string>> { { userNames[0], groupNames.ToList() } };
            await RunTestCore(clientEndpoint, clientAccessTokens,
                () => SendToGroupCore(serviceHubContext, userGroupDict, sendTaskFunc, AddUserToGroupAsync, UserRemoveFromAllGroupsAsync),
                groupNames.Length, receivedMessageDict);
        }
        finally
        {
            await serviceHubContext.DisposeAsync();
        }
    }

    [ConditionalTheory(Skip = "wait for fixing bug of JWT token")]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TestData))]
    internal async Task RemoveConnectionFromAllGroupsTest(ServiceTransportType serviceTransportType, string appName)
    {
        var userNames = GenerateRandomNames(ClientConnectionCount);
        var groupNames = GenerateRandomNames(GroupCount);
        var receivedMessageDict = new ConcurrentDictionary<int, int>();
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
        try
        {
            Func<Task> sendTaskFunc = () => serviceHubContext.Clients.Groups(groupNames).SendAsync(MethodName, Message);

            IList<HubConnection> connections = null;
            CancellationTokenSource cancellationTokenSource = null;

            try
            {
                connections = await CreateAndStartClientConnections(clientEndpoint, clientAccessTokens);
                cancellationTokenSource = new CancellationTokenSource();
                HandleHubConnection(connections, cancellationTokenSource);
                ListenOnMessage(connections, receivedMessageDict);

                var expectedReceivedMessageCount = groupNames.Length;
                var connectionGroupDict = new Dictionary<string, List<string>> { { connections[0].ConnectionId, groupNames.ToList() } };

                Assert.False(cancellationTokenSource.Token.IsCancellationRequested);

                await AddConnectionToGroupAsync(serviceHubContext, connectionGroupDict);
                await Task.Delay(Timeout);
                await sendTaskFunc();
                await Task.Delay(Timeout);
                await ConnectionRemoveFromAllGroupsAsync((ServiceHubContext)serviceHubContext, connectionGroupDict);
                await Task.Delay(Timeout);
                await sendTaskFunc();
                await Task.Delay(Timeout);

                await Task.Delay(Timeout);

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
        finally
        {
            await serviceHubContext.DisposeAsync();
        }
    }

    private static Task ConnectionRemoveFromAllGroupsAsync(ServiceHubContext serviceHubContext, IDictionary<string, List<string>> connectionGroupDict)
    {
        return Task.WhenAll(from connection in connectionGroupDict.Keys
                            select serviceHubContext.Groups.RemoveFromAllGroupsAsync(connection, default));
    }

    private static Task AddConnectionToGroupAsync(IServiceHubContext serviceHubContext, IDictionary<string, List<string>> connectionGroupDict)
    {
        return Task.WhenAll(from connectiongroup in connectionGroupDict
                            select Task.WhenAll(from grp in connectiongroup.Value
                                                select serviceHubContext.Groups.AddToGroupAsync(connectiongroup.Key, grp)));
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Persistent)]
    [InlineData(Management.ServiceTransportType.Transient)]
    internal async Task CheckUserExistenceInGroupTest(ServiceTransportType transportType)
    {
        var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                o.ServiceTransportType = transportType;
            })
            .Build();
        var hubName = nameof(CheckUserExistenceInGroupTest);
        var endpoint = serviceManager.GetClientEndpoint(hubName);
        var group = $"{nameof(CheckUserExistenceInGroupTest)}_group";
        var user = $"{nameof(CheckUserExistenceInGroupTest)}_user";
        var token = serviceManager.GenerateClientAccessToken(hubName, user);
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var serviceHubContext = await serviceManager.CreateHubContextAsync(hubName, loggerFactory);
            var conn = CreateHubConnection(endpoint, token);
            await conn.StartAsync().OrTimeout();
            await Task.Delay(Timeout);
            await serviceHubContext.UserGroups.AddToGroupAsync(user, group).OrTimeout();
            await Task.Delay(Timeout);
            Assert.True(await serviceHubContext.UserGroups.IsUserInGroup(user, group).OrTimeout());
            await serviceHubContext.UserGroups.RemoveFromGroupAsync(user, group).OrTimeout();
            await Task.Delay(Timeout);
            Assert.False(await serviceHubContext.UserGroups.IsUserInGroup(user, group).OrTimeout());
            await conn.StopAsync().OrTimeout();
        }
    }

    [Theory(Skip = "Not Ready")]
    [MemberData(nameof(TestData))]
    internal async Task SendToConnectionTest(ServiceTransportType serviceTransportType, string appName)
    {
        var userNames = GenerateRandomNames(ClientConnectionCount);
        var testServer = _testServerFactory.Create(TestOutputHelper);
        await testServer.StartAsync(new Dictionary<string, string> { [TestStartup.ApplicationName] = appName });

        var task = testServer.HubConnectionManager.WaitForConnectionCountAsync(1);

        var receivedMessageDict = new ConcurrentDictionary<int, int>();
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
        try
        {
            await RunTestCore(clientEndpoint, clientAccessTokens,
                async () =>
                {
                    var connectionId = await task.OrTimeout();
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

        var userNames = GenerateRandomNames(ClientConnectionCount);
        var groupNames = GenerateRandomNames(GroupCount);
        var receivedMessageDict = new ConcurrentDictionary<int, int>();
        var (clientEndpoint, clientAccessTokens, serviceHubContext) = await InitAsync(serviceTransportType, appName, userNames);
        try
        {
            await RunTestCore(clientEndpoint, clientAccessTokens,
                async () =>
                {
                    var connectionId = await task.OrTimeout();
                    await serviceHubContext.Groups.AddToGroupAsync(connectionId, groupNames[0]);
                    await serviceHubContext.Clients.Group(groupNames[0]).SendAsync(MethodName, Message);
                    // We can't guarantee the order between the send group and the following leave group
                    await Task.Delay(Timeout);
                    await serviceHubContext.Groups.RemoveFromGroupAsync(connectionId, groupNames[0]);
                    await serviceHubContext.Clients.Group(groupNames[0]).SendAsync(MethodName, Message);
                },
                1, receivedMessageDict);
        }
        finally
        {
            await serviceHubContext.DisposeAsync();
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(TestData))]
    public async Task CloseConnectionTest(ServiceTransportType serviceTransportType, string appName)
    {
        //when ServiceHubContext.Dispose in persistent mode, there is always an error, so we can not use VerifiableLog
        using (StartLog(out var loggerFactory))
        {
            ServiceHubContext serviceHubContext = null;
            try
            {
                const string reason = "This is a test reason.";
                var serviceManager = new ServiceManagerBuilder()
                    .WithOptions(o =>
                    {
                        o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                        o.ServiceTransportType = serviceTransportType;
                        o.ApplicationName = appName;
                    })
                    .WithLoggerFactory(loggerFactory)
                    .Build();
                serviceHubContext = (await serviceManager.CreateHubContextAsync(HubName)) as ServiceHubContext;
                var negotiationRes = await serviceHubContext.NegotiateAsync(new NegotiationOptions { EnableDetailedErrors = true, IsDiagnosticClient = true });
                var conn = CreateHubConnection(negotiationRes.Url, negotiationRes.AccessToken);
                var tcs = new TaskCompletionSource<string>();
                conn.Closed += ex =>
                {
                    if (ex is null)
                    {
                        tcs.SetException(new InvalidOperationException("close exception is null"));
                    }
                    tcs.SetResult(ex.Message);
                    return Task.CompletedTask;
                };
                await conn.StartAsync();
                await serviceHubContext.ClientManager.CloseConnectionAsync(conn.ConnectionId, reason);

                var actualReason = await tcs.Task.OrTimeout();
                Assert.Contains(reason, actualReason);
            }
            finally
            {
                await serviceHubContext?.DisposeAsync();
            }
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Transient)]
    [InlineData(Management.ServiceTransportType.Persistent)]
    public async Task CheckConnectionExistsTest(ServiceTransportType serviceTransportType)
    {
        //when ServiceHubContext.Dispose in persistent mode, there is always an error, so we can not use VerifiableLog
        ServiceHubContext serviceHubContext = null;
        using (StartLog(out var loggerFactory))
        {
            try
            {
                var serviceManager = new ServiceManagerBuilder()
                    .WithOptions(o =>
                    {
                        o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                        o.ServiceTransportType = serviceTransportType;
                    })
                    .WithLoggerFactory(loggerFactory)
                    .Build();
                serviceHubContext = (await serviceManager.CreateHubContextAsync(HubName)) as ServiceHubContext;
                var negotiationRes = await serviceHubContext.NegotiateAsync();
                var conn = CreateHubConnection(negotiationRes.Url, negotiationRes.AccessToken);
                var tcs = new TaskCompletionSource();
                conn.Closed += ex =>
                {
                    tcs.SetResult();
                    return Task.CompletedTask;
                };
                await conn.StartAsync();
                var connId = conn.ConnectionId;
                var exists = await serviceHubContext.ClientManager.ConnectionExistsAsync(connId);
                Assert.True(exists);

                await serviceHubContext.ClientManager.CloseConnectionAsync(connId);
                await tcs.Task;
                exists = await serviceHubContext.ClientManager.ConnectionExistsAsync(connId);
                Assert.False(exists);
            }
            finally
            {
                await serviceHubContext?.DisposeAsync();
            }
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Transient)]
    [InlineData(Management.ServiceTransportType.Persistent)]
    public async Task CheckUserExistsTest(ServiceTransportType serviceTransportType)
    {
        //when ServiceHubContext.Dispose in persistent mode, there is always an error, so we can not use VerifiableLog
        ServiceHubContext serviceHubContext = null;
        using (StartLog(out var loggerFactory))
        {
            try
            {
                var userId = "TestUser";
                var serviceManager = new ServiceManagerBuilder()
                    .WithOptions(o =>
                    {
                        o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                        o.ServiceTransportType = serviceTransportType;
                    })
                    .WithLoggerFactory(loggerFactory)
                    .Build();
                serviceHubContext = (await serviceManager.CreateHubContextAsync(HubName)) as ServiceHubContext;
                var negotiationRes = await serviceHubContext.NegotiateAsync(new() { UserId = userId });
                var conn = CreateHubConnection(negotiationRes.Url, negotiationRes.AccessToken);
                await conn.StartAsync();
                var tcs = new TaskCompletionSource();
                conn.Closed += ex =>
                {
                    tcs.SetResult();
                    return Task.CompletedTask;
                };
                var exists = await serviceHubContext.ClientManager.UserExistsAsync(userId);
                Assert.True(exists);

                await serviceHubContext.ClientManager.CloseConnectionAsync(conn.ConnectionId);
                await tcs.Task;
                exists = await serviceHubContext.ClientManager.UserExistsAsync(userId);
                Assert.False(exists);
            }
            finally
            {
                await serviceHubContext?.DisposeAsync();
            }
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Transient)]
    [InlineData(Management.ServiceTransportType.Persistent)]
    public async Task CheckGroupExistsTest(ServiceTransportType serviceTransportType)
    {
        //when ServiceHubContext.Dispose in persistent mode, there is always an error, so we can not use VerifiableLog
        ServiceHubContext serviceHubContext = null;
        using (StartLog(out var loggerFactory))
        {
            try
            {
                var groupName = "TestGroup";
                var serviceManager = new ServiceManagerBuilder()
                    .WithOptions(o =>
                    {
                        o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                        o.ServiceTransportType = serviceTransportType;
                    })
                    .WithLoggerFactory(loggerFactory)
                    .Build();
                serviceHubContext = (await serviceManager.CreateHubContextAsync(HubName)) as ServiceHubContext;
                var negotiationRes = await serviceHubContext.NegotiateAsync();
                var conn = CreateHubConnection(negotiationRes.Url, negotiationRes.AccessToken);
                await conn.StartAsync();
                var tcs = new TaskCompletionSource();
                conn.Closed += ex =>
                {
                    tcs.SetResult();
                    return Task.CompletedTask;
                };

                var exists = await serviceHubContext.ClientManager.GroupExistsAsync(groupName);
                Assert.False(exists);

                await serviceHubContext.Groups.AddToGroupAsync(conn.ConnectionId, groupName);
                exists = await serviceHubContext.ClientManager.GroupExistsAsync(groupName);
                Assert.True(exists);

                await serviceHubContext.ClientManager.CloseConnectionAsync(conn.ConnectionId);
                await tcs.Task;
                exists = await serviceHubContext.ClientManager.GroupExistsAsync(groupName);
                Assert.False(exists);
            }
            finally
            {
                await serviceHubContext.DisposeAsync();
            }
        }
    }

    [SkipIfMultiEndpointsAbsentFact]
    internal async Task WithEndpointsTest()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            var services = new ServiceCollection().AddSignalRServiceManager().AddSingleton(loggerFactory).Configure((ServiceManagerOptions o) =>
            {
                o.ServiceTransportType = Management.ServiceTransportType.Persistent;
                o.ServiceEndpoints = TestConfiguration.Instance.Configuration.GetEndpoints(Constants.Keys.AzureSignalREndpointsKey).ToArray();
            });
            var serviceProvider = services.AddSingleton<IReadOnlyCollection<ServiceDescriptor>>(services.ToList()).BuildServiceProvider();
            var hubContext = await serviceProvider.GetRequiredService<IServiceManager>().CreateHubContextAsync(HubName);
            var endpointManager = serviceProvider.GetRequiredService<IServiceEndpointManager>();
            var endpoints = endpointManager.GetEndpoints(HubName).ToArray<ServiceEndpoint>();
            var connections = endpoints.Select(endpoint =>
            {
                var provider = endpointManager.GetEndpointProvider(endpoint);
                var clientEndpoint = provider.GetClientEndpoint(HubName, null, null);
                var token = provider.GenerateClientAccessTokenAsync(HubName).Result;
                return CreateHubConnection(clientEndpoint, token);
            }).ToArray();
            using var cancellationTokenSource = new CancellationTokenSource();
            await Task.WhenAll(connections.Select(conn => conn.StartAsync()));
            HandleHubConnection(connections, cancellationTokenSource);
            var receivedFlags = new bool[endpoints.Length];
            for (var i = 0; i < endpoints.Length; i++)
            {
                var j = i;
                connections[j].On(MethodName, (string message) =>
                {
                    receivedFlags[j] = true;
                });
            }

            var subHubContext = (hubContext as ServiceHubContext).WithEndpoints(endpoints.Take(1));
            await subHubContext.Clients.All.SendAsync(MethodName, Message);
            await Task.Delay(TimeSpan.FromSeconds(10));

            Assert.False(cancellationTokenSource.Token.IsCancellationRequested);
            Assert.True(receivedFlags[0]);
            for (var i = 1; i < receivedFlags.Length; i++)
            {
                Assert.False(receivedFlags[i]);
            }
        }
    }

    [ConditionalFact(Skip = "TODO: move this test into ServiceConnectionContainerBase or WeakConnectionContainer")]
    [SkipIfConnectionStringNotPresent]
    //TODO this test doesn't work anymore.
    //https://github.com/Azure/azure-signalr/pull/707/files  ServiceConnectionContainerBase or WeakConnectionContainer should be tested separately.
    internal async Task StopServiceHubContextTest()
    {
        var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                o.ConnectionCount = 1;
                o.ServiceTransportType = Management.ServiceTransportType.Persistent;
            })
            .Build();
        var serviceHubContext = await serviceManager.CreateHubContextAsync("hub", LoggerFactory);
        var connectionContainer = ((ServiceHubContextImpl)serviceHubContext).ServiceProvider.GetRequiredService<IServiceConnectionContainer>();//TODO
        await serviceHubContext.DisposeAsync();
        await Task.Delay(500);
        Assert.Equal(ServiceConnectionStatus.Disconnected, connectionContainer.Status);
    }

    [ConditionalFact]
    [SkipIfConnectionStringNotPresent]
    public async Task ServiceHubContextIndependencyTest()
    {
        using (var logCollector = StartVerifiableLog(out var loggerFactory, LogLevel.Debug))
        {
            using var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                    o.ServiceTransportType = Management.ServiceTransportType.Persistent;
                })
                .WithLoggerFactory(loggerFactory)
                .Build();
            var hubContext_1 = await serviceManager.CreateHubContextAsync(HubName);
            var hubContext_2 = await serviceManager.CreateHubContextAsync(HubName);
            await hubContext_1.Clients.All.SendAsync(MethodName, Message);
            await hubContext_1.DisposeAsync();
            await hubContext_2.Clients.All.SendAsync(MethodName, Message);
            await hubContext_2.DisposeAsync();

            logCollector.Expects("EndpointOffline");
        }
    }

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(true)]
    [InlineData(false)]
    internal async Task EnableMessageTracingTest(bool enableMessageTracing)
    {
        var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = Management.ServiceTransportType.Persistent;
            o.EnableMessageTracing = enableMessageTracing;
        }).Build();
        var loggerFactory = new TestLoggerFactory();
        var context = await serviceManager.CreateHubContextAsync(HubName, loggerFactory: loggerFactory);
        var user = GenerateRandomNames(1)[0];
        var group = GenerateRandomNames(1)[0];

        try
        {
            await context.UserGroups.AddToGroupAsync(user, group).OrTimeout();
            await Task.Delay(200);
            Assert.Equal(enableMessageTracing, loggerFactory.Logger.EventIds.Contains(new EventId(80, "StartToAddUserToGroup")));
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [ConditionalFact]
    [SkipIfConnectionStringNotPresent]
    public async Task AddNonexistentConnectionToGroupRestApiTest()
    {
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = Management.ServiceTransportType.Transient;
        }).BuildServiceManager();
        using var context = await serviceManager.CreateHubContextAsync(HubName, default);
        await context.Groups.AddToGroupAsync(Guid.NewGuid().ToString(), "group");
    }

    [ConditionalFact]
    [SkipIfConnectionStringNotPresent]
    public async Task CloseNonexistentConnectionToGroupRestApiTest()
    {
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = Management.ServiceTransportType.Transient;
        }).BuildServiceManager();
        using var context = await serviceManager.CreateHubContextAsync(HubName, default);
        await context.ClientManager.CloseConnectionAsync(Guid.NewGuid().ToString());
    }

    [ConditionalFact]
    [SkipIfConnectionStringNotPresent]
    public async Task RemoveNonexistentConnectionFromGroupRestApiTest()
    {
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = Management.ServiceTransportType.Transient;
        }).BuildServiceManager();
        using var context = await serviceManager.CreateHubContextAsync(HubName, default);
        await context.Groups.RemoveFromGroupAsync(Guid.NewGuid().ToString(), "group");
    }

    [ConditionalFact]
    [SkipIfConnectionStringNotPresent]
    public async Task RemoveNonexistentConnectionFromAllGroupsRestApiTest()
    {
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = Management.ServiceTransportType.Transient;
        }).BuildServiceManager();
        using var context = await serviceManager.CreateHubContextAsync(HubName, default);
        await context.Groups.RemoveFromAllGroupsAsync(Guid.NewGuid().ToString());
    }

    [ConditionalFact]
    [SkipIfConnectionStringNotPresent]
    public async Task AddNonexistentUserToGroupRestApiTest()
    {
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = Management.ServiceTransportType.Transient;
        }).BuildServiceManager();
        using var context = await serviceManager.CreateHubContextAsync(HubName, default);
        await context.UserGroups.AddToGroupAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    }

    [ConditionalFact]
    [SkipIfConnectionStringNotPresent]
    public async Task AddNonexistentUserToGroupWithTTLRestApiTest()
    {
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = Management.ServiceTransportType.Transient;
        }).BuildServiceManager();
        using var context = await serviceManager.CreateHubContextAsync(HubName, default);
        await context.UserGroups.AddToGroupAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), TimeSpan.Zero);
        await context.UserGroups.AddToGroupAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), TimeSpan.FromSeconds(1));
    }

    [ConditionalFact]
    [SkipIfConnectionStringNotPresent]
    public async Task RemoveNonexistentUserFromGroupRestApiTest()
    {
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = Management.ServiceTransportType.Transient;
        }).BuildServiceManager();
        using var context = await serviceManager.CreateHubContextAsync(HubName, default);
        await context.UserGroups.RemoveFromGroupAsync(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
    }

    [ConditionalFact]
    [SkipIfConnectionStringNotPresent]
    public async Task RemoveNonexistentUserFromAllGroupsRestApiTest()
    {
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = Management.ServiceTransportType.Transient;
        }).BuildServiceManager();
        using var context = await serviceManager.CreateHubContextAsync(HubName, default);
        await context.UserGroups.RemoveFromAllGroupsAsync(Guid.NewGuid().ToString());
    }

    private static readonly IEnumerable<object[]> ListConnectionsInGroupTestData =
    [
        [6, 6, null, 6, 1],
        [6, 3, null, 3, 1],
        [6, null, 2, 6, 3],
        [6, 5, 2, 5, 3],
    ];
    public static readonly IEnumerable<object[]> ListConnectionsInGroupTestDataWithTransport =
        from serviceTransportType in ServiceTransportType
        from data in ListConnectionsInGroupTestData
        select new object[] { serviceTransportType, data[0], data[1], data[2], data[3], data[4] };

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(ListConnectionsInGroupTestDataWithTransport))]
    public async Task ListConnectionsInGroupTest(ServiceTransportType serviceTransportType, int totalConnectionCount, int? maxCountToList, int? maxPageSize, int expectedTotalCount, int expectedPageCount)
    {
        using var logger = StartLog(out var loggerFactory, nameof(ListConnectionsInGroupTest));
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = serviceTransportType;
            o.HttpClientTimeout = TimeSpan.FromHours(1);
        })
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);
        var groupName = nameof(ListConnectionsInGroupTest) + Guid.NewGuid().ToString();
        var negotationResponse = await hubContext.NegotiateAsync();
        var clientConnections = await CreateAndStartClientConnections(negotationResponse.Url, Enumerable.Repeat(negotationResponse.AccessToken, totalConnectionCount));
        foreach (var connection in clientConnections)
        {
            await hubContext.Groups.AddToGroupAsync(connection.ConnectionId, groupName);
            TestOutputHelper.WriteLine("Created connection: " + connection.ConnectionId);
        }

        var actualPageCount = 0;
        var actualConnectionCount = 0;

        await foreach (var page in hubContext.Groups.ListConnectionsInGroup(groupName, maxCountToList).AsPages(null, maxPageSize))
        {
            //actualPageCount++;
            actualConnectionCount += page.Values.Count;
            actualPageCount++;
            TestOutputHelper.WriteLine($"The {actualPageCount} page:");
            foreach (var connection in page.Values)
            {
                TestOutputHelper.WriteLine($"Listed connection: {connection.ConnectionId}");
            }
        }

        Assert.Equal(expectedPageCount, actualPageCount);
        Assert.Equal(expectedTotalCount, actualConnectionCount);
        foreach (var connection in clientConnections)
        {
            await connection.StopAsync();
        }
    }

    public static readonly IEnumerable<object[]> serviceTransportTypeTestData =
        from serviceTransportType in ServiceTransportType
        select new object[] { serviceTransportType};

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(serviceTransportTypeTestData))]
    public async Task ClientInvocationTest_Default(ServiceTransportType serviceTransportType)
    {
        using var logger = StartLog(out var loggerFactory, nameof(ClientInvocationTest_Default));
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = serviceTransportType;
        })
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        await TestClientInvocationAsync(hubContext, "json");
        if (serviceTransportType == Management.ServiceTransportType.Transient)
        {
            // Only in Transient mode, We will try to convert JSON messages to MessagePack for backward compatibility
            await TestClientInvocationAsync(hubContext, "messagepack");
        }
    }

    //[ConditionalTheory]
    //[SkipIfConnectionStringNotPresent]
    //[MemberData(nameof(serviceTransportTypeTestData))]
    //public async Task ClientInvocationTest_WithMessagePack(ServiceTransportType serviceTransportType)
    //{
    //    using var logger = StartLog(out var loggerFactory, nameof(ClientInvocationTest_WithMessagePack));
    //    using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
    //    {
    //        o.ConnectionString = TestConfiguration.Instance.ConnectionString;
    //        o.ServiceTransportType = serviceTransportType;
    //    })
    //        .WithHubProtocols(new MessagePackHubProtocol())
    //        .WithLoggerFactory(loggerFactory)
    //        .BuildServiceManager();
    //    using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

    //    await TestClientInvocationAsync(hubContext, "messagepack");
    //}

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(serviceTransportTypeTestData))]
    public async Task ClientInvocationTest_WithJson(ServiceTransportType serviceTransportType)
    {
        using var logger = StartLog(out var loggerFactory, nameof(ClientInvocationTest_WithJson));
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = serviceTransportType;
        })
            .WithHubProtocols(new JsonHubProtocol())
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        await TestClientInvocationAsync(hubContext, "json");
    }

    //[ConditionalTheory]
    //[SkipIfConnectionStringNotPresent]
    //[MemberData(nameof(serviceTransportTypeTestData))]
    //public async Task ClientInvocationTest_WithMessagePackWithNewtonSoft(ServiceTransportType serviceTransportType)
    //{
    //    using var logger = StartLog(out var loggerFactory, nameof(ClientInvocationTest_WithMessagePackWithNewtonSoft));
    //    using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
    //    {
    //        o.ConnectionString = TestConfiguration.Instance.ConnectionString;
    //        o.ServiceTransportType = serviceTransportType;
    //    })
    //        .WithHubProtocols(new MessagePackHubProtocol())
    //        .WithNewtonsoftJson()
    //        .WithLoggerFactory(loggerFactory)
    //        .BuildServiceManager();
    //    using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

    //    //if (serviceTransportType == Management.ServiceTransportType.Transient)
    //    //{
    //        await TestClientInvocationAsync(hubContext, "json");
    //    //}
    //    await TestClientInvocationAsync(hubContext, "messagepack");
    //}

    //[ConditionalTheory]
    //[SkipIfConnectionStringNotPresent]
    //[MemberData(nameof(serviceTransportTypeTestData))]
    //public async Task ClientInvocationTest_WithMultipleProtocols(ServiceTransportType serviceTransportType)
    //{
    //    using var logger = StartLog(out var loggerFactory, nameof(ClientInvocationTest_WithMultipleProtocols));
    //    using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
    //    {
    //        o.ConnectionString = TestConfiguration.Instance.ConnectionString;
    //        o.ServiceTransportType = serviceTransportType;
    //    })
    //        .WithHubProtocols(new MessagePackHubProtocol(), new JsonHubProtocol())
    //        .WithLoggerFactory(loggerFactory)
    //        .BuildServiceManager();
    //    using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

    //    await TestClientInvocationAsync(hubContext, "json");
    //    await TestClientInvocationAsync(hubContext, "messagepack");
    //}

    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [MemberData(nameof(serviceTransportTypeTestData))]
    public async Task ClientInvocationTest_WithJsonWithCustomisedSerializer(ServiceTransportType serviceTransportType)
    {
        var options = JsonObjectSerializerHubProtocol.CreateDefaultSerializerSettings();
        options.Converters.Add(new TestEnumJsonConverter());

        var objectSerializer = new JsonObjectSerializer(options);
        var protocol = new JsonObjectSerializerHubProtocol(objectSerializer);

        using var logger = StartLog(out var loggerFactory, nameof(ClientInvocationTest_WithJsonWithCustomisedSerializer));
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ConnectionString = TestConfiguration.Instance.ConnectionString;
            o.ServiceTransportType = serviceTransportType;
            o.ObjectSerializer = objectSerializer;
        })
            .WithHubProtocols(protocol)
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        await TestClientInvocationAsync(hubContext, "json", true);
    }

    //[ConditionalTheory]
    //[SkipIfConnectionStringNotPresent]
    //[MemberData(nameof(serviceTransportTypeTestData))]
    //public async Task ClientInvocationTest_WithMessagepackWithCustomisedSerializer(ServiceTransportType serviceTransportType)
    //{
    //    var messagePackOptions = MessagePackSerializerOptions.Standard
    //.WithResolver(
    //    CompositeResolver.Create(
    //        new IMessagePackFormatter[]
    //        {
    //            new TestEnumFormatter(),
    //        },
    //        new IFormatterResolver[]
    //        {
    //            StandardResolver.Instance,
    //        }));

    //    var objectSerializer = new MessagePackObjectSerializer(messagePackOptions); // an ObjectSerializer wrapper

    //    var protocolOptions = new MessagePackHubProtocolOptions
    //    {
    //        SerializerOptions = messagePackOptions,
    //    };

    //    var messagePackProtocol = new MessagePackObjectSerializerHubProtocol(
    //        objectSerializer,
    //        Extensions.Options.Options.Create(protocolOptions)
    //    );

    //    using var logger = StartLog(out var loggerFactory, nameof(ClientInvocationTest_WithMessagepackWithCustomisedSerializer));
    //    using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
    //    {
    //        o.ConnectionString = TestConfiguration.Instance.ConnectionString;
    //        o.ServiceTransportType = serviceTransportType;
    //        o.ObjectSerializer = objectSerializer;
    //    })
    //        .WithHubProtocols(messagePackProtocol)
    //        .WithLoggerFactory(loggerFactory)
    //        .BuildServiceManager();
    //    using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

    //    await TestClientInvocationAsync(hubContext, "messagepack", true);
    //}

    private static async Task<HubConnection> CreateAndStartClientConnectionWithProtocolAsync(string endpoint, string accessToken, string protocol = "json", bool hasCustomisedSerializer = false)
    {
        var connectionBuilder = new HubConnectionBuilder()
            .WithUrl(endpoint, option =>
            {
                option.AccessTokenProvider = () =>
                {
                    return Task.FromResult(accessToken);
                };
            })
            .WithAutomaticReconnect();

        //var messagePackOptions = MessagePackSerializerOptions.Standard
        //    .WithResolver(
        //        CompositeResolver.Create(
        //            // formatters (our enum formatter first)
        //            new IMessagePackFormatter[]
        //            {
        //                new TestEnumFormatter(),
        //            },
        //            // resolvers
        //            new IFormatterResolver[]
        //            {
        //                StandardResolver.Instance,
        //            }));

        switch (protocol.ToLower())
        {
            case "json":
                if (hasCustomisedSerializer)
                {
                    connectionBuilder.AddJsonProtocol(options =>
                    {
                        options.PayloadSerializerOptions.Converters.Add(new TestEnumJsonConverter());
                    });
                }
                else
                {
                    connectionBuilder.AddJsonProtocol();
                }
                break;
            case "messagepack":
                if (hasCustomisedSerializer)
                {
                    connectionBuilder.AddMessagePackProtocol(
                        options =>
                        {
                            //options.SerializerOptions = messagePackOptions;
                        });
                }
                else
                {
                    connectionBuilder.AddMessagePackProtocol();
                }
                break;
            default:
                throw new ArgumentException($"The protocol '{protocol}' is not supported.");
        }
        var connection = connectionBuilder.Build();

        await connection.StartAsync();

        connection.On("InvokeString", (Func<string, Task<string>>)(args =>
        {
            return Task.FromResult<string>("Method Invoked");
        }));
        connection.On("InvokeObject", (Func<string, Task<object>>)(args =>
        {
            return Task.FromResult<object>(new { result = "Method Invoked" });
        }));
        connection.On("InvokeEnum", (Func<string, Task<TestEnum>>)(args =>
        {
            return Task.FromResult<TestEnum>(TestEnum.MethodInvoked);
        }));
        connection.On("InvokeNull", (Func<string, Task<object>>)(args =>
        {
            return Task.FromResult<object>(null);
        }));
        connection.On("InvokeException", (Func<string, Task<object>>)(args =>
        {
            throw new InvalidOperationException("Test exception");
        }));

        return connection;
    }

    private static async Task TestClientInvocationAsync(ServiceHubContext context, string protocol, bool hasCustomisedSerializer = false)
    {
        var negotationResponse = await context.NegotiateAsync();

        var expectedStringMessage = "Method Invoked";
        var expectedEnumString = "MethodInvoked";
        var expectedCustomisedEnumString = "aaamytest";

        var clientConnection = await CreateAndStartClientConnectionWithProtocolAsync(negotationResponse.Url, negotationResponse.AccessToken, protocol, hasCustomisedSerializer);

        var response_string = await context.Clients.Client(clientConnection.ConnectionId).InvokeAsync<string>("InvokeString", "", default).OrTimeout();
        Assert.Equal(expectedStringMessage, response_string.ToString());

        var response_object = await context.Clients.Client(clientConnection.ConnectionId).InvokeAsync<object>("InvokeObject", "", default).OrTimeout();
        switch (response_object)
        {
            case JsonElement json:
                Assert.Equal(expectedStringMessage, json.GetProperty("result").GetString());
                break;

            case IDictionary<string, object> dictString:
                Assert.Equal(expectedStringMessage, dictString["result"]?.ToString());
                break;

            case IDictionary<object, object> dictObj:
                Assert.Equal(expectedStringMessage, dictObj["result"]?.ToString());
                break;
        }

        var response_null = await context.Clients.Client(clientConnection.ConnectionId).InvokeAsync<object>("InvokeNull", "", default).OrTimeout();
        Assert.Null(response_null);

        var ex = await Assert.ThrowsAsync<HubException>(async () =>
            await context.Clients.Client(clientConnection.ConnectionId).InvokeAsync<object>("InvokeException", "", default).OrTimeout());
        Assert.Contains("Test exception", ex.Message);

        if (hasCustomisedSerializer)
        {
            var response_enum = await context.Clients.Client(clientConnection.ConnectionId).InvokeAsync<TestEnum>("InvokeEnum", "", default);
            Assert.Equal(expectedCustomisedEnumString, response_enum.ToString());
        }
        else
        {
            var response_enum = await context.Clients.Client(clientConnection.ConnectionId).InvokeAsync<TestEnum>("InvokeEnum", "", default);
            Assert.Equal(expectedEnumString, response_enum.ToString());
        }

        await clientConnection.StopAsync();
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

    private static Task AddUserToGroupWithTtlAsync(IServiceHubContext serviceHubContext, IDictionary<string, List<string>> userGroupDict, TimeSpan ttl)
    {
        return Task.WhenAll(from usergroup in userGroupDict
                            select Task.WhenAll(from grp in usergroup.Value
                                                select serviceHubContext.UserGroups.AddToGroupAsync(usergroup.Key, grp, ttl)));
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
        await Task.Delay(Timeout);
        await sendTask();
        await Task.Delay(Timeout);
        await userRemoveFromGroupTask(serviceHubContext, userGroupDict);
        await Task.Delay(Timeout);
        await sendTask();
        await Task.Delay(Timeout);
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
            await Task.Delay(Timeout);

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

    private async Task<(string ClientEndpoint, IEnumerable<string> ClientAccessTokens, IServiceHubContext ServiceHubContext)> InitAsync(ServiceTransportType serviceTransportType, string appName, IEnumerable<string> userNames)
    {
        var serviceManager = GenerateServiceManager(TestConfiguration.Instance.ConnectionString, serviceTransportType, appName);
        var serviceHubContext = await serviceManager.CreateHubContextAsync(HubName, LoggerFactory);

        var clientEndpoint = serviceManager.GetClientEndpoint(HubName);
        var tokens = from userName in userNames
                     select serviceManager.GenerateClientAccessToken(HubName, userName);
        return (clientEndpoint, tokens, serviceHubContext);
    }

    private static string[] GenerateRandomNames(int count)
    {
        var names = new string[count];
        for (var i = 0; i < count; i++)
        {
            names[i] = Guid.NewGuid().ToString();
        }
        return names;
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
        for (var i = 0; i < connections.Count; i++)
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

    private static IServiceManager GenerateServiceManager(string connectionString, ServiceTransportType serviceTransportType = Management.ServiceTransportType.Transient, string appName = null)
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

    private sealed class TestLoggerFactory : ILoggerFactory
    {
        public TestLogger Logger { get; } = new TestLogger();
        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return Logger;
        }

        public void Dispose()
        {
        }

        public sealed class TestLogger : ILogger
        {
            public List<EventId> EventIds = new List<EventId>();

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                EventIds.Add(eventId);
            }
        }
    }

    private sealed class TestEnumFormatter : IMessagePackFormatter<TestEnum>
    {
        public void Serialize(ref MessagePackWriter writer, TestEnum value, MessagePackSerializerOptions options)
        {
            if (value == TestEnum.MethodInvoked)
            {
                writer.Write("aaamytest");
            }
        }

        public TestEnum Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            var name = reader.ReadString();
            return name == "aaamytest"
                ? TestEnum.aaamytest
                : TestEnum.None;
        }
    }

    private enum TestEnum
    {
        None,
        MethodInvoked,
        aaamytest
    }

    private sealed class TestEnumJsonConverter : JsonConverter<TestEnum>
    {
        public override TestEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return TestEnum.None;
            }

            var name = reader.GetString();
            return name == "aaamytest"
                ? TestEnum.aaamytest
                : TestEnum.None;
        }

        public override void Write(Utf8JsonWriter writer, TestEnum value, JsonSerializerOptions options)
        {
            if (value == TestEnum.MethodInvoked)
            {
                writer.WriteStringValue("aaamytest");
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

//    private sealed class MessagePackObjectSerializer : ObjectSerializer
//    {
//        private readonly MessagePackSerializerOptions _options;

//        public MessagePackObjectSerializer(MessagePackSerializerOptions options)
//        {
//            _options = options ?? throw new ArgumentNullException(nameof(options));
//        }

//        public override void Serialize(Stream stream, object value, Type type, CancellationToken cancellationToken)
//        {
//            // MessagePack is sync; we honor the token only for consistency.
//            MessagePackSerializer.Serialize(type, stream, value, _options, cancellationToken: cancellationToken);
//            stream.Flush();
//        }

//        public override async ValueTask SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken)
//        {
//#if NETSTANDARD2_0
//        // Async overloads may not be available; fall back to sync and wrap in Task.
//        Serialize(stream, value, type, cancellationToken);
//        await Task.CompletedTask;
//#else
//            await MessagePackSerializer.SerializeAsync(type, stream, value, _options, cancellationToken);
//            await stream.FlushAsync(cancellationToken);
//#endif
//        }

//        public override object Deserialize(Stream stream, Type returnType, CancellationToken cancellationToken)
//        {
//            return MessagePackSerializer.Deserialize(returnType, stream, _options, cancellationToken: cancellationToken);
//        }

//        public override async ValueTask<object> DeserializeAsync(Stream stream, Type returnType, CancellationToken cancellationToken)
//        {
//#if NETSTANDARD2_0
//        // Async overloads may not be available; fall back to sync.
//        return Deserialize(stream, returnType, cancellationToken);
//#else
//            return await MessagePackSerializer.DeserializeAsync(returnType, stream, _options, cancellationToken);
//#endif
//        }
//    }
}
