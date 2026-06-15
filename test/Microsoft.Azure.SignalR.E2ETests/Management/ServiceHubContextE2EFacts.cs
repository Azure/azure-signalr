// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
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

    #region ClientInvocation Tests

    /// <summary>
    /// Tests client invocation with default protocol configuration using JSON client.
    /// </summary>
    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Transient)]
    [InlineData(Management.ServiceTransportType.Persistent)]
    public async Task ClientInvocation_WithDefaultProtocol_JsonClient(ServiceTransportType serviceTransportType)
    {
        // Arrange: Create service manager with default protocol (no explicit hub protocol configured)
        using var logger = StartLog(out var loggerFactory, nameof(ClientInvocation_WithDefaultProtocol_JsonClient));
        var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                o.ServiceTransportType = serviceTransportType;
            })
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        // Arrange: Create JSON client connection
        var negotiationResponse = await hubContext.NegotiateAsync();
        var clientConnection = await CreateJsonClientConnectionAsync(negotiationResponse.Url, negotiationResponse.AccessToken);

        try
        {
            // Act: Invoke method that returns all test values in a single call
            var result = await hubContext.Clients.Client(clientConnection.ConnectionId)
                .InvokeAsync<TestInvocationResult>("InvokeAll", TestInput, default).OrTimeout();

            // Assert: Verify string value
            Assert.Equal("Method Invoked", result.StringValue);

            // Assert: Verify enum value with standard serialization
            Assert.Equal(TestEnum.MethodInvoked, result.EnumValue);

            // Assert: Verify null value
            Assert.Null(result.NullValue);

            // Assert: Verify datetime value
            Assert.Equal(TestDateTime, result.DateTimeValue);

            // Act & Assert: Invoke method that throws exception
            var ex = await Assert.ThrowsAsync<HubException>(async () =>
                await hubContext.Clients.Client(clientConnection.ConnectionId)
                    .InvokeAsync<object>("InvokeException", TestInput, default).OrTimeout());
            Assert.Contains("Test exception", ex.Message);
        }
        finally
        {
            await clientConnection.StopAsync();
        }
    }

    /// <summary>
    /// Tests client invocation with explicit JSON protocol configured on ServiceManager.
    /// </summary>
    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Transient)]
    [InlineData(Management.ServiceTransportType.Persistent)]
    public async Task ClientInvocation_WithExplicitJsonProtocol(ServiceTransportType serviceTransportType)
    {
        // Arrange: Create service manager with explicit JsonHubProtocol
        using var logger = StartLog(out var loggerFactory, nameof(ClientInvocation_WithExplicitJsonProtocol));
        var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                o.ServiceTransportType = serviceTransportType;
            })
            .WithHubProtocols(new JsonHubProtocol())
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        // Arrange: Create JSON client connection
        var negotiationResponse = await hubContext.NegotiateAsync();
        var clientConnection = await CreateJsonClientConnectionAsync(negotiationResponse.Url, negotiationResponse.AccessToken);

        try
        {
            // Act: Invoke method that returns all test values in a single call
            var result = await hubContext.Clients.Client(clientConnection.ConnectionId)
                .InvokeAsync<TestInvocationResult>("InvokeAll", TestInput, default).OrTimeout();

            // Assert: Verify string value
            Assert.Equal("Method Invoked", result.StringValue);

            // Assert: Verify enum value with standard serialization
            Assert.Equal(TestEnum.MethodInvoked, result.EnumValue);

            // Assert: Verify null value
            Assert.Null(result.NullValue);

            // Assert: Verify datetime value
            Assert.Equal(TestDateTime, result.DateTimeValue);

            // Act & Assert: Invoke method that throws exception
            var ex = await Assert.ThrowsAsync<HubException>(async () =>
                await hubContext.Clients.Client(clientConnection.ConnectionId)
                    .InvokeAsync<object>("InvokeException", TestInput, default).OrTimeout());
            Assert.Contains("Test exception", ex.Message);
        }
        finally
        {
            await clientConnection.StopAsync();
        }
    }

    /// <summary>
    /// Tests client invocation with explicit MessagePack protocol configured on ServiceManager.
    /// </summary>
    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Transient)]
    [InlineData(Management.ServiceTransportType.Persistent)]
    public async Task ClientInvocation_WithExplicitMessagePackProtocol(ServiceTransportType serviceTransportType)
    {
        // Arrange: Create service manager with explicit MessagePackHubProtocol
        using var logger = StartLog(out var loggerFactory, nameof(ClientInvocation_WithExplicitMessagePackProtocol));
        var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                o.ServiceTransportType = serviceTransportType;
            })
            .WithHubProtocols(new MessagePackHubProtocol())
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        // Arrange: Create MessagePack client connection
        var negotiationResponse = await hubContext.NegotiateAsync();
        var clientConnection = await CreateMessagePackClientConnectionAsync(negotiationResponse.Url, negotiationResponse.AccessToken);

        try
        {
            // Act: Invoke method that returns all test values in a single call
            var result = await hubContext.Clients.Client(clientConnection.ConnectionId)
                .InvokeAsync<TestInvocationResult>("InvokeAll", TestInput, default).OrTimeout();

            // Assert: Verify string value
            Assert.Equal("Method Invoked", result.StringValue);

            // Assert: Verify enum value with standard serialization
            Assert.Equal(TestEnum.MethodInvoked, result.EnumValue);

            // Assert: Verify null value
            Assert.Null(result.NullValue);

            // Assert: Verify datetime value
            Assert.Equal(TestDateTime, result.DateTimeValue);

            // Act & Assert: Invoke method that throws exception
            var ex = await Assert.ThrowsAsync<HubException>(async () =>
                await hubContext.Clients.Client(clientConnection.ConnectionId)
                    .InvokeAsync<object>("InvokeException", TestInput, default).OrTimeout());
            Assert.Contains("Test exception", ex.Message);
        }
        finally
        {
            await clientConnection.StopAsync();
        }
    }

    /// <summary>
    /// Tests client invocation with both JSON and MessagePack protocols configured.
    /// Verifies that clients using either protocol can successfully invoke methods.
    /// </summary>
    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Transient)]
    [InlineData(Management.ServiceTransportType.Persistent)]
    public async Task ClientInvocation_WithMultipleProtocols(ServiceTransportType serviceTransportType)
    {
        // Arrange: Create service manager with both JSON and MessagePack protocols
        using var logger = StartLog(out var loggerFactory, nameof(ClientInvocation_WithMultipleProtocols));
        var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                o.ServiceTransportType = serviceTransportType;
            })
            .WithHubProtocols(new JsonHubProtocol(), new MessagePackHubProtocol())
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        // Arrange: Create both JSON and MessagePack client connections
        var negotiationResponse = await hubContext.NegotiateAsync();
        var jsonClient = await CreateJsonClientConnectionAsync(negotiationResponse.Url, negotiationResponse.AccessToken);
        var messagePackClient = await CreateMessagePackClientConnectionAsync(negotiationResponse.Url, negotiationResponse.AccessToken);

        try
        {
            // JSON Client - Act: Invoke method that returns all test values
            var jsonResult = await hubContext.Clients.Client(jsonClient.ConnectionId)
                .InvokeAsync<TestInvocationResult>("InvokeAll", TestInput, default).OrTimeout();

            // Assert: Verify all values for JSON client
            Assert.Equal("Method Invoked", jsonResult.StringValue);
            Assert.Equal(TestEnum.MethodInvoked, jsonResult.EnumValue);
            Assert.Null(jsonResult.NullValue);
            Assert.Equal(TestDateTime, jsonResult.DateTimeValue);

            // Act & Assert: Invoke method that throws exception (JSON)
            var ex_json = await Assert.ThrowsAsync<HubException>(async () =>
                await hubContext.Clients.Client(jsonClient.ConnectionId)
                    .InvokeAsync<object>("InvokeException", TestInput, default).OrTimeout());
            Assert.Contains("Test exception", ex_json.Message);

            // MessagePack Client - Act: Invoke method that returns all test values
            var msgPackResult = await hubContext.Clients.Client(messagePackClient.ConnectionId)
                .InvokeAsync<TestInvocationResult>("InvokeAll", TestInput, default).OrTimeout();

            // Assert: Verify all values for MessagePack client
            Assert.Equal("Method Invoked", msgPackResult.StringValue);
            Assert.Equal(TestEnum.MethodInvoked, msgPackResult.EnumValue);
            Assert.Null(msgPackResult.NullValue);
            Assert.Equal(TestDateTime, msgPackResult.DateTimeValue);

            // Act & Assert: Invoke method that throws exception (MessagePack)
            var ex_messagePack = await Assert.ThrowsAsync<HubException>(async () =>
                await hubContext.Clients.Client(messagePackClient.ConnectionId)
                    .InvokeAsync<object>("InvokeException", TestInput, default).OrTimeout());
            Assert.Contains("Test exception", ex_messagePack.Message);
        }
        finally
        {
            await jsonClient.StopAsync();
            await messagePackClient.StopAsync();
        }
    }

    /// <summary>
    /// Tests client invocation with MessagePack protocol and Newtonsoft.Json serializer for REST payloads.
    /// </summary>
    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Transient)]
    [InlineData(Management.ServiceTransportType.Persistent)]
    public async Task ClientInvocation_WithMessagePackAndNewtonsoftJson(ServiceTransportType serviceTransportType)
    {
        // Arrange: Create service manager with MessagePack protocol and Newtonsoft.Json for REST
        using var logger = StartLog(out var loggerFactory, nameof(ClientInvocation_WithMessagePackAndNewtonsoftJson));
        var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                o.ServiceTransportType = serviceTransportType;
            })
            .WithHubProtocols(new MessagePackHubProtocol())
            .WithNewtonsoftJson()
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        // Arrange: Create MessagePack client connection
        var negotiationResponse = await hubContext.NegotiateAsync();
        var clientConnection = await CreateMessagePackClientConnectionAsync(negotiationResponse.Url, negotiationResponse.AccessToken);

        try
        {
            // Act: Invoke method that returns all test values in a single call
            var result = await hubContext.Clients.Client(clientConnection.ConnectionId)
                .InvokeAsync<TestInvocationResult>("InvokeAll", TestInput, default).OrTimeout();

            // Assert: Verify string value
            Assert.Equal("Method Invoked", result.StringValue);

            // Assert: Verify enum value with standard serialization
            Assert.Equal(TestEnum.MethodInvoked, result.EnumValue);

            // Assert: Verify null value
            Assert.Null(result.NullValue);

            // Assert: Verify datetime value
            Assert.Equal(TestDateTime, result.DateTimeValue);

            // Act & Assert: Invoke method that throws exception
            var ex = await Assert.ThrowsAsync<HubException>(async () =>
                await hubContext.Clients.Client(clientConnection.ConnectionId)
                    .InvokeAsync<object>("InvokeException", TestInput, default).OrTimeout());
            Assert.Contains("Test exception", ex.Message);
        }
        finally
        {
            await clientConnection.StopAsync();
        }
    }

    /// <summary>
    /// Tests client invocation with custom JSON serializer.
    /// Custom serializer converts TestEnum.MethodInvoked to "aaamytest" string.
    /// </summary>
    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Transient)]
    [InlineData(Management.ServiceTransportType.Persistent)]
    public async Task ClientInvocation_WithCustomJsonSerializer(ServiceTransportType serviceTransportType)
    {
        // Arrange: Create custom JSON serializer with TestEnumJsonConverter
        var jsonOptions = JsonObjectSerializerHubProtocol.CreateDefaultSerializerSettings();
        jsonOptions.Converters.Add(new TestEnumJsonConverter());
        var customProtocol = new JsonObjectSerializerHubProtocol(new JsonObjectSerializer(jsonOptions));

        // Arrange: Create service manager with custom JSON protocol
        using var logger = StartLog(out var loggerFactory, nameof(ClientInvocation_WithCustomJsonSerializer));
        var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                o.ServiceTransportType = serviceTransportType;
            })
            .WithHubProtocols(customProtocol)
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        // Arrange: Create JSON client with matching custom serializer
        var negotiationResponse = await hubContext.NegotiateAsync();
        var clientConnection = await CreateJsonClientWithCustomSerializerAsync(negotiationResponse.Url, negotiationResponse.AccessToken);

        try
        {
            // Act: Invoke method that returns all test values in a single call
            var result = await hubContext.Clients.Client(clientConnection.ConnectionId)
                .InvokeAsync<TestInvocationResult>("InvokeAll", TestInput, default).OrTimeout();

            // Assert: Verify string value
            Assert.Equal("Method Invoked", result.StringValue);

            // Assert: Verify enum value with customised serialization (MethodInvoked -> aaamytest)
            Assert.Equal(TestEnum.aaamytest, result.EnumValue);

            // Assert: Verify null value
            Assert.Null(result.NullValue);

            // Assert: Verify datetime value
            Assert.Equal(TestDateTime, result.DateTimeValue);

            // Act & Assert: Invoke method that throws exception
            var ex = await Assert.ThrowsAsync<HubException>(async () =>
                await hubContext.Clients.Client(clientConnection.ConnectionId)
                    .InvokeAsync<object>("InvokeException", TestInput, default).OrTimeout());
            Assert.Contains("Test exception", ex.Message);
        }
        finally
        {
            await clientConnection.StopAsync();
        }
    }

    /// <summary>
    /// Tests client invocation with custom MessagePack serializer.
    /// Custom serializer converts TestEnum.MethodInvoked to "aaamytest" string.
    /// </summary>
    [ConditionalTheory]
    [SkipIfConnectionStringNotPresent]
    [InlineData(Management.ServiceTransportType.Transient)]
    [InlineData(Management.ServiceTransportType.Persistent)]
    public async Task ClientInvocation_WithCustomMessagePackSerializer(ServiceTransportType serviceTransportType)
    {
        // Arrange: Create custom MessagePack protocol with TestEnumFormatter and TestInvocationResultFormatter
        var customProtocol = CreateMessagePackProtocolWithCustomSerializer();

        // Arrange: Create service manager with custom MessagePack protocol
        using var logger = StartLog(out var loggerFactory, nameof(ClientInvocation_WithCustomMessagePackSerializer));
        var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                o.ServiceTransportType = serviceTransportType;
            })
            .WithHubProtocols(customProtocol)
            .WithLoggerFactory(loggerFactory)
            .BuildServiceManager();
        using var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);

        // Arrange: Create MessagePack client with matching custom serializer
        var negotiationResponse = await hubContext.NegotiateAsync();
        var clientConnection = await CreateMessagePackClientWithCustomSerializerAsync(negotiationResponse.Url, negotiationResponse.AccessToken);

        try
        {
            // Act: Invoke method that returns all test values in a single call
            var result = await hubContext.Clients.Client(clientConnection.ConnectionId)
                .InvokeAsync<TestInvocationResult>("InvokeAll", TestInput, default).OrTimeout();

            // Assert: Verify string value
            Assert.Equal("Method Invoked", result.StringValue);

            // Assert: Verify enum value with customised serialization (MethodInvoked -> aaamytest)
            Assert.Equal(TestEnum.aaamytest, result.EnumValue);

            // Assert: Verify null value
            Assert.Null(result.NullValue);

            // Assert: Verify datetime value
            Assert.Equal(TestDateTime, result.DateTimeValue);

            // Act & Assert: Invoke method that throws exception
            var ex = await Assert.ThrowsAsync<HubException>(async () =>
                await hubContext.Clients.Client(clientConnection.ConnectionId)
                    .InvokeAsync<object>("InvokeException", TestInput, default).OrTimeout());
            Assert.Contains("Test exception", ex.Message);
        }
        finally
        {
            await clientConnection.StopAsync();
        }
    }

    #endregion

    #region Client Connection Helpers (setup/teardown plumbing)

    private static async Task<HubConnection> CreateJsonClientConnectionAsync(string endpoint, string accessToken)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(endpoint, option => option.AccessTokenProvider = () => Task.FromResult(accessToken))
            .WithAutomaticReconnect()
            .AddJsonProtocol()
            .Build();

        await connection.StartAsync();
        RegisterClientInvocationHandlers(connection);
        return connection;
    }

    private static async Task<HubConnection> CreateMessagePackClientConnectionAsync(string endpoint, string accessToken)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(endpoint, option => option.AccessTokenProvider = () => Task.FromResult(accessToken))
            .WithAutomaticReconnect()
            .AddMessagePackProtocol()
            .Build();

        await connection.StartAsync();
        RegisterClientInvocationHandlers(connection);
        return connection;
    }

    private static async Task<HubConnection> CreateJsonClientWithCustomSerializerAsync(string endpoint, string accessToken)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(endpoint, option => option.AccessTokenProvider = () => Task.FromResult(accessToken))
            .WithAutomaticReconnect()
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new TestEnumJsonConverter()))
            .Build();

        await connection.StartAsync();
        RegisterClientInvocationHandlers(connection);
        return connection;
    }

    private static async Task<HubConnection> CreateMessagePackClientWithCustomSerializerAsync(string endpoint, string accessToken)
    {
        var messagePackOptions = MessagePackSerializerOptions.Standard.WithResolver(
            CompositeResolver.Create(
                new IMessagePackFormatter[] { new TestEnumFormatter(), new TestInvocationResultFormatter(), new TestInvocationInputFormatter() },
                new IFormatterResolver[] { StandardResolver.Instance }));

        var connection = new HubConnectionBuilder()
            .WithUrl(endpoint, option => option.AccessTokenProvider = () => Task.FromResult(accessToken))
            .WithAutomaticReconnect()
            .AddMessagePackProtocol(options => options.SerializerOptions = messagePackOptions)
            .Build();

        await connection.StartAsync();
        RegisterClientInvocationHandlers(connection);
        return connection;
    }

    private static readonly DateTime TestDateTime = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

    private static readonly TestInvocationInput TestInput = new TestInvocationInput
    {
        StringValue = "Test Input String",
        DateTimeValue = TestDateTime,
        IntValue = 42
    };

    private static void RegisterClientInvocationHandlers(HubConnection connection)
    {
        connection.On("InvokeAll", (Func<TestInvocationInput, Task<TestInvocationResult>>)(input =>
        {
            // Validate input was correctly deserialized
            if (input.StringValue != TestInput.StringValue ||
                input.DateTimeValue != TestInput.DateTimeValue ||
                input.IntValue != TestInput.IntValue)
            {
                throw new InvalidOperationException($"Input validation failed. Expected: {TestInput.StringValue}, {TestInput.DateTimeValue}, {TestInput.IntValue}. Actual: {input.StringValue}, {input.DateTimeValue}, {input.IntValue}");
            }

            return Task.FromResult(new TestInvocationResult
            {
                StringValue = "Method Invoked",
                EnumValue = TestEnum.MethodInvoked,
                NullValue = null,
                DateTimeValue = TestDateTime
            });
        }));
        connection.On("InvokeException", (Func<TestInvocationInput, Task<object>>)(_ => throw new InvalidOperationException("Test exception")));
    }

    private static MessagePackHubProtocol CreateMessagePackProtocolWithCustomSerializer()
    {
        var messagePackOptions = MessagePackSerializerOptions.Standard.WithResolver(
            CompositeResolver.Create(
                new IMessagePackFormatter[] { new TestEnumFormatter(), new TestInvocationResultFormatter(), new TestInvocationInputFormatter() },
                new IFormatterResolver[] { StandardResolver.Instance }));

        return new MessagePackHubProtocol(
            Extensions.Options.Options.Create(new MessagePackHubProtocolOptions { SerializerOptions = messagePackOptions }));
    }

    #endregion

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

    internal sealed class TestEnumFormatter : IMessagePackFormatter<TestEnum>
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

    internal sealed class TestInvocationResultFormatter : IMessagePackFormatter<TestInvocationResult>
    {
        public void Serialize(ref MessagePackWriter writer, TestInvocationResult value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(4);

            writer.Write("StringValue");
            writer.Write(value.StringValue);

            writer.Write("EnumValue");
            var resolver = options.Resolver;
            var enumFormatter = resolver.GetFormatterWithVerify<TestEnum>();
            enumFormatter.Serialize(ref writer, value.EnumValue, options);

            writer.Write("NullValue");
            writer.WriteNil();

            writer.Write("DateTimeValue");
            writer.Write(value.DateTimeValue.Ticks);
        }

        public TestInvocationResult Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            var count = reader.ReadMapHeader();

            var result = new TestInvocationResult();
            var resolver = options.Resolver;
            var enumFormatter = resolver.GetFormatterWithVerify<TestEnum>();

            for (var i = 0; i < count; i++)
            {
                var propertyName = reader.ReadString();

                switch (propertyName)
                {
                    case "StringValue":
                        result.StringValue = reader.ReadString();
                        break;
                    case "EnumValue":
                        result.EnumValue = enumFormatter.Deserialize(ref reader, options);
                        break;
                    case "NullValue":
                        reader.TryReadNil();
                        result.NullValue = null;
                        break;
                    case "DateTimeValue":
                        result.DateTimeValue = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return result;
        }
    }

    internal sealed class TestInvocationInputFormatter : IMessagePackFormatter<TestInvocationInput>
    {
        public void Serialize(ref MessagePackWriter writer, TestInvocationInput value, MessagePackSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteMapHeader(3);

            writer.Write("StringValue");
            writer.Write(value.StringValue);

            writer.Write("DateTimeValue");
            writer.Write(value.DateTimeValue.Ticks);

            writer.Write("IntValue");
            writer.Write(value.IntValue);
        }

        public TestInvocationInput Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
            {
                return null;
            }

            var count = reader.ReadMapHeader();

            var result = new TestInvocationInput();

            for (var i = 0; i < count; i++)
            {
                var propertyName = reader.ReadString();

                switch (propertyName)
                {
                    case "StringValue":
                        result.StringValue = reader.ReadString();
                        break;
                    case "DateTimeValue":
                        result.DateTimeValue = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
                        break;
                    case "IntValue":
                        result.IntValue = reader.ReadInt32();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return result;
        }
    }

    public sealed class TestInvocationInput
    {
        public string StringValue { get; set; }
        public DateTime DateTimeValue { get; set; }
        public int IntValue { get; set; }
    }

    public sealed class TestInvocationResult
    {
        public string StringValue { get; set; }
        public TestEnum EnumValue { get; set; }
        public object NullValue { get; set; }
        public DateTime DateTimeValue { get; set; }
    }

    public enum TestEnum
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

    private sealed class MessagePackObjectSerializer : ObjectSerializer
    {
        private readonly MessagePackSerializerOptions _options;

        public MessagePackObjectSerializer(MessagePackSerializerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public override void Serialize(Stream stream, object value, Type type, CancellationToken cancellationToken)
        {
            // MessagePack is sync; we honor the token only for consistency.
            MessagePackSerializer.Serialize(type, stream, value, _options, cancellationToken: cancellationToken);
            stream.Flush();
        }

        public override async ValueTask SerializeAsync(Stream stream, object value, Type type, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0
        // Async overloads may not be available; fall back to sync and wrap in Task.
        Serialize(stream, value, type, cancellationToken);
        await Task.CompletedTask;
#else
            await MessagePackSerializer.SerializeAsync(type, stream, value, _options, cancellationToken);
            await stream.FlushAsync(cancellationToken);
#endif
        }

        public override object Deserialize(Stream stream, Type returnType, CancellationToken cancellationToken)
        {
            return MessagePackSerializer.Deserialize(returnType, stream, _options, cancellationToken: cancellationToken);
        }

        public override async ValueTask<object> DeserializeAsync(Stream stream, Type returnType, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0
        // Async overloads may not be available; fall back to sync.
        return Deserialize(stream, returnType, cancellationToken);
#else
            return await MessagePackSerializer.DeserializeAsync(returnType, stream, _options, cancellationToken);
#endif
        }
    }
}
