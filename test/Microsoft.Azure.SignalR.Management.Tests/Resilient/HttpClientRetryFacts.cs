// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Moq.Protected;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests;

using HttpHandlerSetup = Moq.Language.Flow.ISetup<HttpMessageHandler, Task<HttpResponseMessage>>;


public class HttpClientRetryFacts
{
    private const string HubName = "hub";
    private static readonly Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>>[] TransientHttpErrorSetup = new Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>>[]
    {
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway)),
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout)),
        // Simulate timeout 
        (setup) => setup.Returns<HttpRequestMessage, CancellationToken>(async (request, token) =>
        {
            await Task.Delay(-1, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }),
    };

    public static readonly IEnumerable<object[]> NoRetryTestData = TransientHttpErrorSetup.Zip(new Action<Exception>[]
    {
        void (ex) => Assert.IsType<AzureSignalRRuntimeException>(ex),
        void (ex) => Assert.IsType<AzureSignalRRuntimeException>(ex),
        void (ex) => Assert.IsType<AzureSignalRRuntimeException>(ex),
        void (ex) =>
        {
            var canceled = Assert.IsType<TaskCanceledException>(ex);
            Assert.IsType<TimeoutException>(canceled.InnerException);
        }
    }, (setup, assert) => new object[] { setup, assert });

    [Theory]
    [MemberData(nameof(NoRetryTestData))]
    public async Task NoRetryTest(Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>> setup, Action<Exception> assert)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        setup(handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()));
        var hubContext = await new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                o.HttpClientTimeout = TimeSpan.FromMilliseconds(1);
            })
            .ConfigureServices(services => services
                .AddHttpClient(Constants.HttpClientNames.Resilient)
                .ConfigurePrimaryHttpMessageHandler(sp => handlerMock.Object))
            .BuildServiceManager()
            .CreateHubContextAsync(HubName, default);
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => hubContext.ClientManager.GroupExistsAsync("groupName"));
        assert(exception);
    }

    public static readonly IEnumerable<object[]> FixedDelayRetryTestData = TransientHttpErrorSetup.Zip(new Action<AggregateException>[]
    {
        void (ex) => Assert.All(ex.InnerExceptions,inner=> Assert.IsType<AzureSignalRRuntimeException>(inner)),
        void (ex) => Assert.All(ex.InnerExceptions,inner=> Assert.IsType<AzureSignalRRuntimeException>(inner)),
        void (ex) => Assert.All(ex.InnerExceptions,inner=> Assert.IsType<AzureSignalRRuntimeException>(inner)),
        void (ex) => Assert.All(ex.InnerExceptions,inner=>
        {
            var operationCanceled = Assert.IsType<TaskCanceledException>(inner);
            Assert.IsType<TimeoutException>(operationCanceled.InnerException);
        }),
    }, (setup, assert) => new object[] { setup, assert });

    [Theory]
    [MemberData(nameof(FixedDelayRetryTestData))]
    public async Task FixedDelayRetryTest(Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>> setup, Action<AggregateException> assert)
    {
        var callTimes = new List<DateTime>();
        var handlerMock = new Mock<HttpMessageHandler>();
        setup(handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()))
            .Callback(() => callTimes.Add(DateTime.UtcNow));

        var hubContext = await new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                o.HttpClientTimeout = TimeSpan.FromMilliseconds(1);
                o.RetryOptions = new RetryOptions
                {
                    Mode = RetryMode.Fixed,
                    Delay = TimeSpan.FromMilliseconds(50),
                    MaxRetries = 3
                };
            })
            .ConfigureServices(services => services
                .AddHttpClient(Constants.HttpClientNames.Resilient)
                .ConfigurePrimaryHttpMessageHandler(sp => handlerMock.Object))
            .BuildServiceManager()
            .CreateHubContextAsync(HubName, default);
        var exception = await Assert.ThrowsAnyAsync<AggregateException>(() => hubContext.ClientManager.GroupExistsAsync("groupName"));
        assert(exception);

        handlerMock.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task TheSecondRetrySuccessTest()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));
        var hubContext = await new ServiceManagerBuilder()
             .WithOptions(o =>
             {
                 o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                 o.HttpClientTimeout = TimeSpan.FromMilliseconds(1);
                 o.RetryOptions = new RetryOptions
                 {
                     Mode = RetryMode.Fixed,
                     Delay = TimeSpan.FromMilliseconds(50),
                     MaxRetries = 3
                 };
             })
             .ConfigureServices(services => services
                 .AddHttpClient(Constants.HttpClientNames.Resilient)
                 .ConfigurePrimaryHttpMessageHandler(sp => handlerMock.Object))
             .BuildServiceManager()
             .CreateHubContextAsync(HubName, default);
        await hubContext.ClientManager.GroupExistsAsync("groupName");
        handlerMock.Protected().Verify("SendAsync", Times.Exactly(2), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }
}
