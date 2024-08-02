// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
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
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: e => true,
            logChecker: s =>
            {
                Assert.Single(s);
                Assert.Equal("EndpointOffline", s[0].Write.EventId.Name);
                return true;
            }))
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
        }
    }

    [Fact]
    public void TestStrongConnectionStatus()
    {
        using (StartVerifiableLog(out var loggerFactory, LogLevel.Warning, expectedErrors: e => true,
            logChecker: s =>
            {
                Assert.Single(s);
                Assert.Equal("EndpointOffline", s[0].Write.EventId.Name);
                return true;
            }))
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
        }
    }
}
