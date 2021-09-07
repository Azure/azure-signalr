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
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class RestHealthCheckServiceFacts : LoggedTest
    {
        private const string HubName = "hub";
        public static IEnumerable<object[]> TestRestClientFactoryData
        {
            get
            {
                yield return new object[] { new TestRestClientFactory("userAgent", HttpStatusCode.BadGateway) };
                yield return new object[] { new TestRestClientFactory("userAgent", HttpStatusCode.NotFound) };
                yield return new object[] { new TestRestClientFactory("userAgent", (req, token) => throw new HttpRequestException()) };
            }
        }
        public RestHealthCheckServiceFacts(ITestOutputHelper output = null) : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(TestRestClientFactoryData))]
        internal async Task TestRestHealthCheckServiceWithUnhealthyEndpoint(RestClientFactory implementationInstance)
        {
            using var _ = StartLog(out var loggerFactory);
            using var serviceHubContext = await new ServiceManagerBuilder()
                .WithOptions(o => o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single())
                .WithLoggerFactory(loggerFactory)
                .ConfigureServices(services =>
                {
                    services.AddSingleton(implementationInstance);
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
                .AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => handlerMock.Object).Services
                .AddSignalRServiceManager()
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
    }
}
