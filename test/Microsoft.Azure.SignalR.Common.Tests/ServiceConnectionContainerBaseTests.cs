// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Common.Tests;

public class ServiceConnectionContainerBaseTests(ITestOutputHelper output) : VerifiableLoggedTest(output)
{
    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(1, 2, 3)]
    [InlineData(2, 4, 5)]
    [InlineData(3, 8, 9)]
    [InlineData(4, 16, 17)]
    [InlineData(5, 32, 33)]
    [InlineData(6, 60, 61)]
    [InlineData(600, 60, 61)]
    public void TestGetRetryDelay(int count, int minSeconds, int maxSeconds)
    {
        var c = count;
        var span = ServiceConnectionContainerBase.GetRetryDelay(c);
        Assert.True(TimeSpan.FromSeconds(minSeconds) <= span);
        Assert.True(TimeSpan.FromSeconds(maxSeconds) >= span);
    }

    [Fact]
    public void TestWeakConnectionStatus()
    {
        using (var logCollector = StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
        {
            var endpoint1 = new TestHubServiceEndpoint();
            var conn1 = new TestServiceConnection();
            var scf = new TestServiceConnectionFactory(endpoint1 => conn1);
            var container = new WeakServiceConnectionContainer(scf, 5, endpoint1, loggerFactory.CreateLogger(nameof(TestWeakConnectionStatus)));

            // When init, consider the endpoint as online
            // TODO: improve the logic
            Assert.True(endpoint1.Online);

            conn1.SetStatus(ServiceConnectionStatus.Connecting);
            Assert.True(endpoint1.Online);

            conn1.SetStatus(ServiceConnectionStatus.Connected);
            Assert.True(endpoint1.Online);

            conn1.SetStatus(ServiceConnectionStatus.Disconnected);
            Assert.False(endpoint1.Online);

            conn1.SetStatus(ServiceConnectionStatus.Connecting);
            Assert.False(endpoint1.Online);

            conn1.SetStatus(ServiceConnectionStatus.Connected);
            Assert.True(endpoint1.Online);
            logCollector.Expects("EndpointOffline");
        }
    }

    [Fact]
    public void TestStrongConnectionStatus()
    {
        using (var logCollector = StartVerifiableLog(out var loggerFactory, LogLevel.Warning))
        {
            var endpoint1 = new TestHubServiceEndpoint();
            var conn1 = new TestServiceConnection();
            var scf = new TestServiceConnectionFactory(endpoint1 => conn1);
            var container = new StrongServiceConnectionContainer(scf, 5, null, endpoint1, loggerFactory.CreateLogger(nameof(TestStrongConnectionStatus)));

            // When init, consider the endpoint as online
            // TODO: improve the logic
            Assert.True(endpoint1.Online);

            conn1.SetStatus(ServiceConnectionStatus.Connecting);
            Assert.True(endpoint1.Online);

            conn1.SetStatus(ServiceConnectionStatus.Connected);
            Assert.True(endpoint1.Online);

            conn1.SetStatus(ServiceConnectionStatus.Disconnected);
            Assert.False(endpoint1.Online);

            conn1.SetStatus(ServiceConnectionStatus.Connecting);
            Assert.False(endpoint1.Online);

            conn1.SetStatus(ServiceConnectionStatus.Connected);
            Assert.True(endpoint1.Online);
            logCollector.Expects("EndpointOffline");
        }
    }

    [Fact]
    public async Task TestInvokeAsync()
    {
        var endpoint1 = new TestHubServiceEndpoint();
        var conn1 = new TestServiceConnection();
        var scf = new TestServiceConnectionFactory(endpoint1 => conn1);
        var container = new WeakServiceConnectionContainer(scf, 5, endpoint1, Mock.Of<ILogger>());
        var queryMessage = new GroupMemberQueryMessage() { GroupName = "group" };
        var invokeTask = container.InvokeAsync<GroupMemberQueryResponse>(queryMessage, default);

        var expectedResponse = new GroupMemberQueryResponse()
        {
            ContinuationToken = "abc",
            Members = [new() { ConnectionId = "1" }, new() { ConnectionId = "2" }]
        };
        var buffer = new ArrayBufferWriter<byte>();
        new ServiceProtocol().WriteMessagePayload(expectedResponse, buffer);
        AckHandler.Singleton.TriggerAck(queryMessage.AckId, AckStatus.Ok, new ReadOnlySequence<byte>(buffer.WrittenMemory));
        var response = await invokeTask;
        Assert.Equal(queryMessage, conn1.ReceivedMessages.Single());
        Assert.Equal(expectedResponse.ContinuationToken, response.ContinuationToken);
        Assert.True(expectedResponse.Members.SequenceEqual(response.Members));
    }

    [Fact]
    public async Task TestListConnectionsInGroupAsync()
    {
        var conn = new TestServiceConnection();
        var groupName = "groupName";
        var top = 3;
        var tracingId = (ulong)1;
        var connectionContainerMock = new Mock<ServiceConnectionContainerBase>(
             new TestServiceConnectionFactory(endpoint => conn),
            5,
            new TestHubServiceEndpoint(),
            null,
            Mock.Of<ILogger>(),
            null);
        connectionContainerMock.SetupSequence(c => c.InvokeAsync<GroupMemberQueryResponse>(
            It.IsAny<ServiceMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GroupMemberQueryResponse() { ContinuationToken = "abc", Members = [new() { ConnectionId = "1" }, new() { ConnectionId = "2" }] })
            .ReturnsAsync(new GroupMemberQueryResponse() { ContinuationToken = null, Members = [new() { ConnectionId = "3" }] });
        var enumerator = connectionContainerMock.Object
            .ListConnectionsInGroupAsync(groupName, top, tracingId)
            .GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("1", enumerator.Current.ConnectionId);
        connectionContainerMock.Verify(c => c.InvokeAsync<GroupMemberQueryResponse>(
            It.Is<GroupMemberQueryMessage>(m => m.GroupName == groupName && m.Top == 3 && m.TracingId == tracingId), It.IsAny<CancellationToken>()), Times.Once);
        connectionContainerMock.Invocations.Clear();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("3", enumerator.Current.ConnectionId);
        connectionContainerMock.Verify(c => c.InvokeAsync<GroupMemberQueryResponse>(
    It.Is<GroupMemberQueryMessage>(m => m.GroupName == groupName && m.Top == 1 && m.TracingId == tracingId && m.ContinuationToken == "abc"), It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(await enumerator.MoveNextAsync());
    }
}
