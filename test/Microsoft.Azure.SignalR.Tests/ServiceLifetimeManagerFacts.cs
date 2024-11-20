// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

using SignalRProtocol = Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests;

public class ServiceLifetimeManagerFacts
{
    internal static readonly IOptions<HubOptions<TestHub>> LocalHubOptions = Options.Create(new HubOptions<TestHub>() { SupportedProtocols = new List<string>() { "json", "messagepack" } });

    internal static readonly ILogger<ServiceLifetimeManager<TestHub>> Logger = NullLogger<ServiceLifetimeManager<TestHub>>.Instance;

    internal static readonly AzureSignalRMarkerService Marker = new AzureSignalRMarkerService();

    protected static readonly IHubProtocolResolver HubProtocolResolver =
        new DefaultHubProtocolResolver(new SignalRProtocol.IHubProtocol[]
            {
                new SignalRProtocol.JsonHubProtocol(),
                new SignalRProtocol.MessagePackHubProtocol()
            },
            NullLogger<DefaultHubProtocolResolver>.Instance);

    protected static readonly IOptions<HubOptions> GlobalHubOptions = Options.Create(new HubOptions() { SupportedProtocols = new List<string>() { "json", "messagepack" } });

    private const string MockProtocol = "blazorpack";

    private const string TestMethod = "TestMethod";

    private static readonly List<string> TestUsers = new List<string> { "user1", "user2" };

    private static readonly List<string> TestGroups = new List<string> { "group1", "group2" };

    private static readonly object[] TestArgs = { "TestArgs" };

    private static readonly List<string> TestConnectionIds = new List<string> { "connection1", "connection2" };

    public ServiceLifetimeManagerFacts()
    {
        Marker.IsConfigured = true;
    }

    [Theory]
    [InlineData("SendAllAsync", typeof(BroadcastDataMessage))]
    [InlineData("SendAllExceptAsync", typeof(BroadcastDataMessage))]
    [InlineData("SendConnectionsAsync", typeof(MultiConnectionDataMessage))]
    [InlineData("SendGroupsAsync", typeof(MultiGroupBroadcastDataMessage))]
    [InlineData("SendUserAsync", typeof(UserDataMessage))]
    [InlineData("SendUsersAsync", typeof(MultiUserDataMessage))]
    public async void ServiceLifetimeManagerTest(string functionName, Type type)
    {
        var serviceConnectionManager = new TestServiceConnectionManager<TestHub>();
        var blazorDetector = new DefaultBlazorDetector();
        var clientInvocationManager = new DefaultClientInvocationManager();
        var serviceLifetimeManager = new ServiceLifetimeManager<TestHub>(serviceConnectionManager,
            new ClientConnectionManager(), HubProtocolResolver, Logger, Marker, GlobalHubOptions, LocalHubOptions, blazorDetector, new DefaultServerNameProvider(), clientInvocationManager);

        await InvokeMethod(serviceLifetimeManager, functionName);

        Assert.Equal(1, serviceConnectionManager.GetCallCount(type));
        VerifyServiceMessage(functionName, serviceConnectionManager.ServiceMessage);
        Assert.Equal(2, (serviceConnectionManager.ServiceMessage as MulticastDataMessage).Payloads.Count);
        Assert.False(blazorDetector.IsBlazor(nameof(TestHub)));
    }

    [Theory]
    [InlineData("SendGroupAsync", typeof(GroupBroadcastDataMessage))]
    [InlineData("SendGroupExceptAsync", typeof(GroupBroadcastDataMessage))]
    [InlineData("AddToGroupAsync", typeof(JoinGroupWithAckMessage))]
    [InlineData("RemoveFromGroupAsync", typeof(LeaveGroupWithAckMessage))]
    public async void ServiceLifetimeManagerGroupTest(string functionName, Type type)
    {
        var serviceConnectionManager = new TestServiceConnectionManager<TestHub>();
        var blazorDetector = new DefaultBlazorDetector();
        var clientInvocationManager = new DefaultClientInvocationManager();
        var serviceLifetimeManager = new ServiceLifetimeManager<TestHub>(
            serviceConnectionManager,
            new ClientConnectionManager(),
            HubProtocolResolver,
            Logger,
            Marker,
            GlobalHubOptions,
            LocalHubOptions,
            blazorDetector,
            new DefaultServerNameProvider(),
            clientInvocationManager
            );

        await InvokeMethod(serviceLifetimeManager, functionName);

        Assert.Equal(1, serviceConnectionManager.GetCallCount(type));
        VerifyServiceMessage(functionName, serviceConnectionManager.ServiceMessage);
        Assert.False(blazorDetector.IsBlazor(nameof(TestHub)));
    }

    [Theory]
    [InlineData("SendAllAsync", typeof(BroadcastDataMessage))]
    [InlineData("SendAllExceptAsync", typeof(BroadcastDataMessage))]
    [InlineData("SendConnectionAsync", typeof(MultiConnectionDataMessage))]
    [InlineData("SendConnectionsAsync", typeof(MultiConnectionDataMessage))]
    [InlineData("SendGroupAsync", typeof(GroupBroadcastDataMessage))]
    [InlineData("SendGroupsAsync", typeof(MultiGroupBroadcastDataMessage))]
    [InlineData("SendGroupExceptAsync", typeof(GroupBroadcastDataMessage))]
    [InlineData("SendUserAsync", typeof(UserDataMessage))]
    [InlineData("SendUsersAsync", typeof(MultiUserDataMessage))]
    [InlineData("AddToGroupAsync", typeof(JoinGroupWithAckMessage))]
    [InlineData("RemoveFromGroupAsync", typeof(LeaveGroupWithAckMessage))]
    public async void ServiceLifetimeManagerIntegrationTest(string methodName, Type messageType)
    {
        var proxy = new ServiceConnectionProxy();
        var blazorDetector = new DefaultBlazorDetector();

        var serviceConnectionManager = new ServiceConnectionManager<TestHub>();
        serviceConnectionManager.SetServiceConnection(proxy.ServiceConnectionContainer);
        var clientInvocationManager = new DefaultClientInvocationManager();

        var serviceLifetimeManager = new ServiceLifetimeManager<TestHub>(serviceConnectionManager,
            proxy.ClientConnectionManager, HubProtocolResolver, Logger, Marker, GlobalHubOptions, LocalHubOptions, blazorDetector, new DefaultServerNameProvider(), clientInvocationManager);

        var serverTask = proxy.WaitForServerConnectionAsync(1);
        _ = proxy.StartAsync();
        await proxy.WaitForServerConnectionsInited().OrTimeout();
        await serverTask.OrTimeout();

        var task = proxy.WaitForApplicationMessageAsync(messageType);

        var invokeTask = InvokeMethod(serviceLifetimeManager, methodName);

        var message = await task.OrTimeout();

        if (typeof(IAckableMessage).IsAssignableFrom(messageType))
        {
            var ackId = (message as IAckableMessage).AckId;
            Assert.NotEqual(0, ackId);
            await proxy.WriteMessageAsync(new AckMessage(ackId, (int)AckStatus.Ok));
        }

        // Need to return in time, or it indicate a timeout when sending ack-able messages.
        await invokeTask.OrTimeout();

        VerifyServiceMessage(methodName, message);
    }

    [Theory]
    [InlineData("SendAllAsync", typeof(BroadcastDataMessage))]
    [InlineData("SendAllExceptAsync", typeof(BroadcastDataMessage))]
    [InlineData("SendConnectionsAsync", typeof(MultiConnectionDataMessage))]
    [InlineData("SendGroupsAsync", typeof(MultiGroupBroadcastDataMessage))]
    [InlineData("SendUserAsync", typeof(UserDataMessage))]
    [InlineData("SendUsersAsync", typeof(MultiUserDataMessage))]
    public async void ServiceLifetimeManagerIgnoreBlazorHubProtocolTest(string functionName, Type type)
    {
        var blazorDetector = new DefaultBlazorDetector();
        var protocolResolver = new DefaultHubProtocolResolver(new SignalRProtocol.IHubProtocol[]
            {
                new SignalRProtocol.JsonHubProtocol(),
                new SignalRProtocol.MessagePackHubProtocol(),
                new CustomHubProtocol(),
            },
            NullLogger<DefaultHubProtocolResolver>.Instance);
        var globalHubOptions = Options.Create(new HubOptions() { SupportedProtocols = new List<string>() { "json", "messagepack", MockProtocol, "json" } });
        var localHubOptions = Options.Create(new HubOptions<TestHub>() { SupportedProtocols = new List<string>() { "json", "messagepack", MockProtocol } });
        var serviceConnectionManager = new TestServiceConnectionManager<TestHub>();
        var clientInvocationManager = new DefaultClientInvocationManager();
        var serviceLifetimeManager = new ServiceLifetimeManager<TestHub>(serviceConnectionManager,
            new ClientConnectionManager(), protocolResolver, Logger, Marker, globalHubOptions, localHubOptions, blazorDetector, new DefaultServerNameProvider(), clientInvocationManager);

        await InvokeMethod(serviceLifetimeManager, functionName);

        Assert.Equal(1, serviceConnectionManager.GetCallCount(type));
        VerifyServiceMessage(functionName, serviceConnectionManager.ServiceMessage);
        Assert.Equal(2, (serviceConnectionManager.ServiceMessage as MulticastDataMessage).Payloads.Count);
        Assert.True(blazorDetector.IsBlazor(nameof(TestHub)));
    }

    [Theory]
    [InlineData("SendAllAsync", typeof(BroadcastDataMessage))]
    [InlineData("SendAllExceptAsync", typeof(BroadcastDataMessage))]
    [InlineData("SendConnectionsAsync", typeof(MultiConnectionDataMessage))]
    [InlineData("SendGroupsAsync", typeof(MultiGroupBroadcastDataMessage))]
    [InlineData("SendUserAsync", typeof(UserDataMessage))]
    [InlineData("SendUsersAsync", typeof(MultiUserDataMessage))]
    public async void ServiceLifetimeManagerOnlyBlazorHubProtocolTest(string functionName, Type type)
    {
        var serviceConnectionManager = new TestServiceConnectionManager<TestHub>();
        var blazorDetector = new DefaultBlazorDetector();
        var serviceLifetimeManager = MockLifetimeManager(serviceConnectionManager, null, blazorDetector);

        await InvokeMethod(serviceLifetimeManager, functionName);

        Assert.Equal(1, serviceConnectionManager.GetCallCount(type));
        VerifyServiceMessage(functionName, serviceConnectionManager.ServiceMessage);
        Assert.Equal(1, (serviceConnectionManager.ServiceMessage as MulticastDataMessage).Payloads.Count);
        Assert.True(blazorDetector.IsBlazor(nameof(TestHub)));
    }

    [Fact]
    public async void TestSendConnectionAsyncisOverwrittenWhenClientConnectionExisted()
    {
        var serviceConnectionManager = new TestServiceConnectionManager<TestHub>();
        var clientConnectionManager = new ClientConnectionManager();

        var clientConnection = new ClientConnectionContext(new OpenConnectionMessage("conn1", Array.Empty<Claim>()));
        var serviceConnection = new TestServiceConnectionPrivate();
        clientConnection.ServiceConnection = serviceConnection;
        clientConnectionManager.TryAddClientConnection(clientConnection);

        var manager = MockLifetimeManager(serviceConnectionManager, clientConnectionManager);

        await manager.SendConnectionAsync("conn1", "foo", new object[] { 1, 2 });

        Assert.NotNull(serviceConnection.LastMessage);
        if (serviceConnection.LastMessage is MultiConnectionDataMessage m)
        {
            Assert.Equal("conn1", m.ConnectionList[0]);
            Assert.Equal(1, m.Payloads.Count);
            Assert.True(m.Payloads.ContainsKey(MockProtocol));
            return;
        }
        Assert.True(false);
    }

    [Fact]
    public async void SetUserIdTest()
    {
        var connectionContext = new TestConnectionContext();
        connectionContext.Features.Set(new ServiceUserIdFeature("testUser"));

        var hubConnectionContext = new HubConnectionContext(connectionContext, new(), NullLoggerFactory.Instance);
        var serviceLifetimeManager = MockLifetimeManager(new TestServiceConnectionManager<TestHub>());
        await serviceLifetimeManager.OnConnectedAsync(hubConnectionContext);

        Assert.Equal("testUser", hubConnectionContext.UserIdentifier);
    }

    [Fact]
    public async void DoNotSetUserIdWithoutFeatureTest()
    {
        var connectionContext = new TestConnectionContext();

        var hubConnectionContext = new HubConnectionContext(connectionContext, new(), NullLoggerFactory.Instance);
        var serviceLifetimeManager = MockLifetimeManager(new TestServiceConnectionManager<TestHub>());
        await serviceLifetimeManager.OnConnectedAsync(hubConnectionContext);

        Assert.Null(hubConnectionContext.UserIdentifier);
        Assert.Null(hubConnectionContext.Features.Get<ServiceUserIdFeature>());
    }

    private static async Task InvokeMethod(HubLifetimeManager<TestHub> serviceLifetimeManager, string methodName)
    {
        switch (methodName)
        {
            case "SendAllAsync":
                await serviceLifetimeManager.SendAllAsync(TestMethod, TestArgs);
                break;
            case "SendAllExceptAsync":
                await serviceLifetimeManager.SendAllExceptAsync(TestMethod, TestArgs, TestConnectionIds);
                break;
            case "SendConnectionAsync":
                await serviceLifetimeManager.SendConnectionAsync(TestConnectionIds[0], TestMethod, TestArgs);
                break;
            case "SendConnectionsAsync":
                await serviceLifetimeManager.SendConnectionsAsync(TestConnectionIds, TestMethod, TestArgs);
                break;
            case "SendGroupAsync":
                await serviceLifetimeManager.SendGroupAsync(TestGroups[0], TestMethod, TestArgs);
                break;
            case "SendGroupsAsync":
                await serviceLifetimeManager.SendGroupsAsync(TestGroups, TestMethod, TestArgs);
                break;
            case "SendGroupExceptAsync":
                await serviceLifetimeManager.SendGroupExceptAsync(TestGroups[0], TestMethod, TestArgs,
                    TestConnectionIds);
                break;
            case "SendUserAsync":
                await serviceLifetimeManager.SendUserAsync(TestUsers[0], TestMethod, TestArgs);
                break;
            case "SendUsersAsync":
                await serviceLifetimeManager.SendUsersAsync(TestUsers, TestMethod, TestArgs);
                break;
            case "AddToGroupAsync":
                await serviceLifetimeManager.AddToGroupAsync(TestConnectionIds[0], TestGroups[0]);
                break;
            case "RemoveFromGroupAsync":
                await serviceLifetimeManager.RemoveFromGroupAsync(TestConnectionIds[0], TestGroups[0]);
                break;
            default:
                break;
        }
    }

    private static void VerifyServiceMessage(string methodName, ServiceMessage serviceMessage)
    {
        switch (methodName)
        {
            case "SendAllAsync":
                Assert.Empty(((BroadcastDataMessage)serviceMessage).ExcludedList);
                break;
            case "SendAllExceptAsync":
                Assert.Equal(TestConnectionIds, ((BroadcastDataMessage)serviceMessage).ExcludedList);
                break;
            case "SendConnectionAsync":
                Assert.Equal(TestConnectionIds[0], ((MultiConnectionDataMessage)serviceMessage).ConnectionList[0]);
                break;
            case "SendConnectionsAsync":
                Assert.Equal(TestConnectionIds, ((MultiConnectionDataMessage)serviceMessage).ConnectionList);
                break;
            case "SendGroupAsync":
                Assert.Equal(TestGroups[0], ((GroupBroadcastDataMessage)serviceMessage).GroupName);
                Assert.Empty(((GroupBroadcastDataMessage)serviceMessage).ExcludedList);
                break;
            case "SendGroupsAsync":
                Assert.Equal(TestGroups, ((MultiGroupBroadcastDataMessage)serviceMessage).GroupList);
                break;
            case "SendGroupExceptAsync":
                Assert.Equal(TestGroups[0], ((GroupBroadcastDataMessage)serviceMessage).GroupName);
                Assert.Equal(TestConnectionIds, ((GroupBroadcastDataMessage)serviceMessage).ExcludedList);
                break;
            case "SendUserAsync":
                Assert.Equal(TestUsers[0], ((UserDataMessage)serviceMessage).UserId);
                break;
            case "SendUsersAsync":
                Assert.Equal(TestUsers, ((MultiUserDataMessage)serviceMessage).UserList);
                break;
            case "AddToGroupAsync":
                Assert.Equal(TestConnectionIds[0], ((JoinGroupWithAckMessage)serviceMessage).ConnectionId);
                Assert.Equal(TestGroups[0], ((JoinGroupWithAckMessage)serviceMessage).GroupName);
                break;
            case "RemoveFromGroupAsync":
                Assert.Equal(TestConnectionIds[0], ((LeaveGroupWithAckMessage)serviceMessage).ConnectionId);
                Assert.Equal(TestGroups[0], ((LeaveGroupWithAckMessage)serviceMessage).GroupName);
                break;
            default:
                break;
        }
    }

    private static HubLifetimeManager<TestHub> MockLifetimeManager(IServiceConnectionManager<TestHub> serviceConnectionManager, IClientConnectionManager clientConnectionManager = null, IBlazorDetector blazorDetector = null)
    {
        clientConnectionManager ??= new ClientConnectionManager();

        var protocolResolver = new DefaultHubProtocolResolver(new SignalRProtocol.IHubProtocol[]
            {
                new SignalRProtocol.JsonHubProtocol(),
                new SignalRProtocol.MessagePackHubProtocol(),
                new CustomHubProtocol(),
            },
            NullLogger<DefaultHubProtocolResolver>.Instance
        );
        var globalHubOptions = Options.Create(new HubOptions() { SupportedProtocols = new List<string>() { MockProtocol } });
        var localHubOptions = Options.Create(new HubOptions<TestHub>() { SupportedProtocols = new List<string>() { MockProtocol } });

        var clientInvocationManager = new DefaultClientInvocationManager();

        return new ServiceLifetimeManager<TestHub>(
            serviceConnectionManager,
            clientConnectionManager,
            protocolResolver,
            Logger,
            Marker,
            globalHubOptions,
            localHubOptions,
            blazorDetector,
            new DefaultServerNameProvider(),
            clientInvocationManager
        );
    }

    private sealed class TestServiceConnectionPrivate : TestServiceConnection
    {
        public ServiceMessage LastMessage { get; private set; }

        public override Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
        {
            LastMessage = serviceMessage;
            return Task.FromResult(true);
        }
    }

    private sealed class CustomHubProtocol : SignalRProtocol.IHubProtocol
    {
        public string Name => MockProtocol;

        public TransferFormat TransferFormat => throw new NotImplementedException();

        public int Version => throw new NotImplementedException();

        public ReadOnlyMemory<byte> GetMessageBytes(SignalRProtocol.HubMessage message) => ""u8.ToArray();

        public bool IsVersionSupported(int version)
        {
            throw new NotImplementedException();
        }

        public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, out SignalRProtocol.HubMessage message)
        {
            throw new NotImplementedException();
        }

        public void WriteMessage(SignalRProtocol.HubMessage message, IBufferWriter<byte> output)
        {
            throw new NotImplementedException();
        }
    }
}
