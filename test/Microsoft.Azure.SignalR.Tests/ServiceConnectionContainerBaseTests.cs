// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests;

public class ServiceConnectionContainerBaseTests : VerifiableLoggedTest
{
    public ServiceConnectionContainerBaseTests(ITestOutputHelper helper) : base(helper)
    { }

    [Theory]
    [InlineData(3, 3, 0)]
    [InlineData(0, 1, 1)] // stop more than start will log warn
    [InlineData(1, 2, 1)] // stop more than start will log warn
    [InlineData(3, 1, 0)]
    public async Task TestServersPing(int startCount, int stopCount, int expectedWarn)
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug, logChecker: logs =>
        {
            var warns = logs.Where(s => s.Write.EventId.Name == "TimerAlreadyStopped").ToList();
            Assert.Equal(expectedWarn, warns.Count);
            if (expectedWarn > 0)
            {
                Assert.Contains(warns, s => s.Write.Message.Contains("Failed to stop Servers timer as it's not started"));
            }
            return true;
        }))
        {
            var connections = new List<IServiceConnection>
            {
                new SimpleTestServiceConnection(),
                new SimpleTestServiceConnection(),
                new SimpleTestServiceConnection()
            };
            using var container =
                new TestServiceConnectionContainer(
                    connections,
                    factory: new SimpleTestServiceConnectionFactory(),
                    logger: loggerFactory.CreateLogger<TestServiceConnectionContainer>());

            await container.StartAsync();
            await container.ConnectionInitializedTask;
            var tasks = new List<Task>();

            while (startCount > 0)
            {
                tasks.Add(container.StartGetServersPing());
                startCount--;
            }
            await Task.WhenAll(tasks);

            // default interval is 5s, add 2s for delay, validate any one connection write servers ping.
            if (tasks.Count > 0)
            {
                await Task.WhenAny(connections.Select(c =>
                {
                    var connection = c as SimpleTestServiceConnection;
                    return connection.ServersPingTask.OrTimeout(7000);
                }));
            }

            tasks.Clear();
            while (stopCount > 0)
            {
                tasks.Add(container.StopGetServersPing());
                stopCount--;
            }
            await Task.WhenAll(tasks);
        }
    }

    [Theory]
    [InlineData(1, 1, 3, 3, 0)]
    [InlineData(1, 1, 0, 1, 1)]
    [InlineData(1, 1, 1, 0, 0)]
    [InlineData(1, 3, 2, 2, 2)] // first time error stop won't break second time write.
    public async Task TestServersPingWorkSecondTime(int firstStart, int firstStop, int secondStart, int secondStop, int expectedWarn)
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Debug, logChecker: logs =>
        {
            var warns = logs.Where(s => s.Write.EventId.Name == "TimerAlreadyStopped").ToList();
            Assert.Equal(expectedWarn, warns.Count);
            if (expectedWarn > 0)
            {
                Assert.Contains(warns, s => s.Write.Message.Contains("Failed to stop Servers timer as it's not started"));
            }
            return true;
        }))
        {
            var connections = new List<IServiceConnection>
            {
                new SimpleTestServiceConnection(),
                new SimpleTestServiceConnection(),
                new SimpleTestServiceConnection()
            };
            using var container =
                new TestServiceConnectionContainer(
                    connections,
                    factory: new SimpleTestServiceConnectionFactory(),
                    logger: loggerFactory.CreateLogger<TestServiceConnectionContainer>());

            await container.StartAsync();
            await container.ConnectionInitializedTask;

            var tasks = new List<Task>();

            // first time scale
            while (firstStart > 0)
            {
                tasks.Add(container.StartGetServersPing());
                firstStart--;
            }
            await Task.WhenAll(tasks);

            tasks.Clear();
            while (firstStop > 0)
            {
                tasks.Add(container.StopGetServersPing());
                firstStop--;
            }
            await Task.WhenAll(tasks);

            // second time scale
            tasks.Clear();
            while (secondStart > 0)
            {
                tasks.Add(container.StartGetServersPing());
                secondStart--;
            }
            await Task.WhenAll(tasks);

            // default interval is 5s, add 2s for delay, validate any one connection write servers ping.
            if (tasks.Count > 0)
            {
                await Task.WhenAny(connections.Select(c =>
                {
                    var connection = c as SimpleTestServiceConnection;
                    return connection.ServersPingTask.OrTimeout(7000);
                }));
            }

            tasks.Clear();
            while (secondStop > 0)
            {
                tasks.Add(container.StopGetServersPing());
                secondStop--;
            }
            await Task.WhenAll(tasks);
        }
    }

    [Theory]
    [InlineData(ServiceConnectionStatus.Disconnected)]
    [InlineData(ServiceConnectionStatus.Connected)]
    [InlineData(ServiceConnectionStatus.Connecting)]
    [InlineData(ServiceConnectionStatus.Inited)]
    internal async Task TestIfConnectionWillNotRestartAfterShutdown(ServiceConnectionStatus status)
    {
        var connections = new List<IServiceConnection>
        {
            new SimpleTestServiceConnection(),
            new SimpleTestServiceConnection(status: status)
        };

        var connection = connections[1];

        using var container = new TestServiceConnectionContainer(connections, factory: new SimpleTestServiceConnectionFactory());
        container.ShutdownForTest();

        await container.OnConnectionCompleteForTestShutdown(connection);

        // the connection should not be replaced when shutting down
        Assert.Equal(container.Connections[1], connection);

        // its status is not changed
        Assert.Equal(status, container.Connections[1].Status);

        // the container is not listening to the connection's status changes after shutdown
        Assert.Equal(1, (connection as SimpleTestServiceConnection).ConnectionStatusChangedRemoveCount);
    }

    [Theory]
    [InlineData(GracefulShutdownMode.Off)]
    [InlineData(GracefulShutdownMode.WaitForClientsClose)]
    [InlineData(GracefulShutdownMode.MigrateClients)]
    internal async Task TestOffline(GracefulShutdownMode mode)
    {
        var connections = new List<IServiceConnection>
        {
            new SimpleTestServiceConnection(),
            new SimpleTestServiceConnection()
        };
        using var container = new TestServiceConnectionContainer(connections, factory: new SimpleTestServiceConnectionFactory());

        foreach (SimpleTestServiceConnection c in connections)
        {
            Assert.False(c.ConnectionOfflineTask.IsCompleted);
        }

        await container.OfflineAsync(mode);

        foreach (SimpleTestServiceConnection c in connections)
        {
            Assert.True(c.ConnectionOfflineTask.IsCompleted);
        }
    }

    private sealed class SimpleTestServiceConnectionFactory : IServiceConnectionFactory
    {
        public IServiceConnection Create(HubServiceEndpoint endpoint, IServiceMessageHandler serviceMessageHandler, AckHandler ackHandler, ServiceConnectionType type) => new SimpleTestServiceConnection();
    }

    private sealed class SimpleTestServiceConnection : IServiceConnection
    {
        private readonly TaskCompletionSource<bool> _offline = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _serversPing = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ConnectionInitializedTask => Task.Delay(TimeSpan.FromSeconds(1));

        public ServiceConnectionStatus Status { get; set; } = ServiceConnectionStatus.Disconnected;

        public Task ConnectionOfflineTask => _offline.Task;

        public Task ServersPingTask => _serversPing.Task;

        public int ConnectionStatusChangedAddCount { get; set; }

        public int ConnectionStatusChangedRemoveCount { get; set; }

        public string ConnectionId => throw new NotImplementedException();

        public string ServerId => throw new NotImplementedException();

        public SimpleTestServiceConnection(ServiceConnectionStatus status = ServiceConnectionStatus.Disconnected)
        {
            Status = status;
        }

        public event Action<StatusChange> ConnectionStatusChanged
        {
            add => ConnectionStatusChangedAddCount++;
            remove => ConnectionStatusChangedRemoveCount++;
        }

        public Task StartAsync(string target = null)
        {
            Status = ServiceConnectionStatus.Connected;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            if (RuntimeServicePingMessage.IsFin(serviceMessage))
            {
                _offline.SetResult(true);
            }
            if (RuntimeServicePingMessage.IsGetServers(serviceMessage))
            {
                _serversPing.SetResult(true);
            }
            return Task.CompletedTask;
        }

        public async Task<bool> SafeWriteAsync(ServiceMessage serviceMessage)
        {
            try
            {
                await WriteAsync(serviceMessage);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
