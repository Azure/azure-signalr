// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class RestHealthCheckServiceFacts : LoggedTest
    {
        private const string HubName = "hub";
        public static IEnumerable<object[]> HttpClientMockData
        {
            get
            {
                yield return new object[] { new TestRootHandler(HttpStatusCode.BadGateway) };
                yield return new object[] { new TestRootHandler(HttpStatusCode.NotFound) };
                yield return new object[] { new TestRootHandler((request, token) => throw new HttpRequestException()) };
            }
        }
        public RestHealthCheckServiceFacts(ITestOutputHelper output = null) : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(HttpClientMockData))]
        internal async Task TestRestHealthCheckServiceWithUnhealthyEndpoint(TestRootHandler testHandler)
        {
            using var _ = StartLog(out var loggerFactory);
            using var serviceHubContext = await new ServiceManagerBuilder()
                .WithOptions(o => o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single())
                .WithLoggerFactory(loggerFactory)
                .ConfigureServices(services =>
                {
                    services.AddHttpClient(Constants.HttpClientNames.InternalDefault)
                            .ConfigurePrimaryHttpMessageHandler(() => testHandler);
                    services.Configure<HealthCheckOption>(o => o.EnabledForSingleEndpoint = true);
                })
                .BuildServiceManager()
                .CreateHubContextAsync(HubName, default);
            var endpoint = (serviceHubContext as ServiceHubContextImpl).ServiceProvider.GetRequiredService<IServiceEndpointManager>().GetEndpoints(HubName).First();
            Assert.False(endpoint.Online);
        }

        [Fact]
        public async Task TestRestHealthCheckServiceWithEndpointFromHealthyToUnhealthy()
        {
            var handlerMock = new Mock<DelegatingHandler>();
            // mock health api calls first return healthy then unhealthy.
            handlerMock.Protected().SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
               .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway))
               .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway))
               .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway));

            var checkInterval = TimeSpan.FromSeconds(3);
            var retryInterval = TimeSpan.FromSeconds(0.5);
            using var _ = StartLog(out var loggerFactory);
            var services = new ServiceCollection()
                .AddSignalRServiceManager()
                .AddHttpClient(Constants.HttpClientNames.InternalDefault).ConfigurePrimaryHttpMessageHandler(() => handlerMock.Object).Services
                .Configure<HealthCheckOption>(o =>
                {
                    o.CheckInterval = checkInterval;
                    o.RetryInterval = retryInterval;
                    o.EnabledForSingleEndpoint = true;
                });
            using var serviceHubContext = await new ServiceManagerBuilder(services)
                .WithOptions(o => o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single())
                .WithLoggerFactory(loggerFactory)
                .BuildServiceManager()
                .CreateHubContextAsync(HubName, default);

            var endpoint = (serviceHubContext as ServiceHubContextImpl).ServiceProvider.GetRequiredService<IServiceEndpointManager>().GetEndpoints(HubName).First();

            //The first health check is OK
            Assert.True(endpoint.Online);

            var retryTime = RestHealthCheckService.MaxRetries * retryInterval;
            //Wait until the next health check finish
            await Task.Delay(checkInterval + retryTime + TimeSpan.FromSeconds(1));
            Assert.False(endpoint.Online);
        }

        [Fact]
        public async Task TestTimeoutAsync()
        {
            using var _ = StartLog(out var loggerFactory);
            var taskSource = new TaskCompletionSource();
            var services = new ServiceCollection()
                .AddSignalRServiceManager()
                .AddHttpClient(Constants.HttpClientNames.InternalDefault).ConfigurePrimaryHttpMessageHandler(() => new TestRootHandler(async (message, token) =>
                {
                    try
                    {
                        await Task.Delay(-1, token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        taskSource.SetResult();
                    }
                })).Services
                .Configure<HealthCheckOption>(o =>
                {
                    // Never retry
                    o.RetryInterval = Timeout.InfiniteTimeSpan;
                    // Make the timeout happens as soon as quickly.
                    o.HttpTimeout = TimeSpan.FromMilliseconds(1);
                    o.EnabledForSingleEndpoint = true;
                });
            using var serviceHubContext = await new ServiceManagerBuilder(services)
                .WithOptions(o => o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single())
                .WithLoggerFactory(loggerFactory)
                .BuildServiceManager()
                .CreateHubContextAsync(HubName, default);

            await taskSource.Task.OrTimeout();
        }
    }
}
