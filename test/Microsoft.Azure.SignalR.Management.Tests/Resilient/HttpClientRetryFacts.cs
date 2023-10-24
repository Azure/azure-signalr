// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
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
    private static readonly Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>>[] NonMessageApiTransientHttpErrorSetup = new Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>>[]
    {
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway)),
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.RequestTimeout)),
        // Simulate timeout 
        (setup) => setup.Returns<HttpRequestMessage, CancellationToken>(async (request, token) =>
        {
            await Task.Delay(-1, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })
    };

    public static readonly IEnumerable<object[]> NullRetryOptionsTestData = NonMessageApiTransientHttpErrorSetup.Zip(new Action<Exception>[]
    {
        void (ex) => Assert.IsType<AzureSignalRRuntimeException>(ex),
        void (ex) => Assert.IsType<AzureSignalRRuntimeException>(ex),
        void (ex) => Assert.IsType<AzureSignalRRuntimeException>(ex),
        void (ex) =>
        {
            var canceled = Assert.IsType<TaskCanceledException>(ex);
            Assert.IsType<TimeoutException>(canceled.InnerException);
        },
    }, (setup, assert) => new object[] { setup, assert });

    [Theory]
    [MemberData(nameof(NullRetryOptionsTestData))]
    public async Task NullRetryOptionsTest(Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>> setup, Action<Exception> assert)
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

    public static readonly Func<ServiceHubContext, Task>[] NonMessageApis = new Func<ServiceHubContext, Task>[]
    {
        hubContext=>hubContext.Groups.AddToGroupAsync("connectionId", "groupName"),
        hubContext=>hubContext.Groups.RemoveFromGroupAsync("connectionId", "groupName"),
        hubContext=>hubContext.Groups.RemoveFromAllGroupsAsync("connectionId"),
        hubContext=>hubContext.UserGroups.AddToGroupAsync("userId", "groupName"),
        hubContext=>hubContext.UserGroups.RemoveFromGroupAsync("userId", "groupName"),
        hubContext=>hubContext.UserGroups.RemoveFromAllGroupsAsync("userId"),
        hubContext=>hubContext.ClientManager.GroupExistsAsync("groupName"),
        hubContext=>hubContext.ClientManager.UserExistsAsync("userId"),
        hubContext=>hubContext.ClientManager.ConnectionExistsAsync("connectionId"),
        hubContext=>hubContext.ClientManager.CloseConnectionAsync("connectionId", "reason"),
    };

    public static readonly IEnumerable<object[]> FixedDelayRetryTestData =
        from pair in NonMessageApiTransientHttpErrorSetup.Zip(new Action<AggregateException>[]
            {
                void (ex) => Assert.All(ex.InnerExceptions,inner=> Assert.IsType<AzureSignalRRuntimeException>(inner)),
                void (ex) => Assert.All(ex.InnerExceptions,inner=> Assert.IsType<AzureSignalRRuntimeException>(inner)),
                void (ex) => Assert.All(ex.InnerExceptions,inner=> Assert.IsType<AzureSignalRRuntimeException>(inner)),
                void (ex) => Assert.All(ex.InnerExceptions,inner=>
                {
                    var operationCanceled = Assert.IsType<TaskCanceledException>(inner);
                    Assert.IsType<TimeoutException>(operationCanceled.InnerException);
                }),
            })
        from api in NonMessageApis
        select new object[] { pair.First, pair.Second, api };

    [Theory]
    [MemberData(nameof(FixedDelayRetryTestData))]
    public async Task FixedDelayRetryTestNonMessageApi(Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>> setup, Action<AggregateException> assert, Func<ServiceHubContext, Task> api)
    {
        await FixedDelayRetryTestCore(setup, assert, api, Constants.HttpClientNames.Resilient);
    }

    private static readonly Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>>[] MessageApiTransientHttpErrorSetup = new Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>>[]
    {
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadGateway))
    };

    public static readonly Func<ServiceHubContext, Task>[] MessageApis = new Func<ServiceHubContext, Task>[]
    {
        hubContext=>hubContext.Clients.All.SendAsync("method"),
        hubContext=>hubContext.Clients.Client("abc").SendAsync("method"),
        hubContext=>hubContext.Clients.Group("groupName").SendAsync("method"),
        hubContext=>hubContext.Clients.User("userName").SendAsync("method"),
    };

    public static readonly IEnumerable<object[]> FixedDelayRetryTestMessageApiTestData =
        from setup in MessageApiTransientHttpErrorSetup
        from api in MessageApis
        select new object[] { setup, api };

    [Theory]
    [MemberData(nameof(FixedDelayRetryTestMessageApiTestData))]
    public async Task FixedDelayRetryTestMessageApi(Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>> setup, Func<ServiceHubContext, Task> api)
    {
        await FixedDelayRetryTestCore(setup, void (ex) => Assert.All(ex.InnerExceptions, e => Assert.IsType<AzureSignalRRuntimeException>(e)), api, Constants.HttpClientNames.MessageResilient);
    }

    private static async Task FixedDelayRetryTestCore(Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>> setup, Action<AggregateException> assert, Func<ServiceHubContext, Task> testAction, string httpClientName)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        setup(handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()));

        var hubContext = await new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                o.HttpClientTimeout = TimeSpan.FromMilliseconds(1000);
                o.RetryOptions = new ServiceManagerRetryOptions
                {
                    Mode = ServiceManagerRetryMode.Fixed,
                    Delay = TimeSpan.FromMilliseconds(50),
                    MaxRetries = 3
                };
            })
            .ConfigureServices(services => services
                .AddHttpClient(httpClientName)
                .ConfigurePrimaryHttpMessageHandler(sp => handlerMock.Object))
            .BuildServiceManager()
            .CreateHubContextAsync(HubName, default);
        var exception = await Assert.ThrowsAnyAsync<AzureSignalRRuntimeException>(() => testAction(hubContext));
        var aggregationException = Assert.IsType<AggregateException>(exception.InnerException);
        assert(aggregationException);

        handlerMock.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    private static readonly Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>>[] NonMessageApi_NotTransientHttpErrorSetup = new Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>>[]
    {
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)),
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound)),
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized))
    };

    public static IEnumerable<object[]> NotRetryable_RetryTestNonMessageApiTestData =
        from pair in NonMessageApi_NotTransientHttpErrorSetup.Zip(new Action<Exception>[]
        {
            void (ex) => Assert.IsType<AzureSignalRInvalidArgumentException>(ex),
            void (ex) => Assert.IsType<AzureSignalRInaccessibleEndpointException>(ex),
            void (ex) => Assert.IsType<AzureSignalRUnauthorizedException>(ex)
        })
        from api in NonMessageApis
        select new object[] { pair.First, pair.Second, api };

    [Theory]
    [MemberData(nameof(NotRetryable_RetryTestNonMessageApiTestData))]
    public async Task NonRetryableError_RetryTestNonMessageApi(Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>> setup, Action<Exception> assert, Func<ServiceHubContext, Task> api)
    {
        await NonRetryableError_RetryTestCore(setup, assert, api, Constants.HttpClientNames.Resilient);
    }

    private static readonly Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>>[] MessageApi_NotTransientHttpErrorSetup = new Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>>[]
    {
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)),
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)),
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NotFound)),
        (setup) => setup.ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Unauthorized)),
        // Simulate timeout 
        (setup) => setup.Returns<HttpRequestMessage, CancellationToken>(async (request, token) =>
        {
            await Task.Delay(-1, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        })
    };

    public static IEnumerable<object[]> NotRetryable_RetryTest_MessageApiTestData =
    from pair in MessageApi_NotTransientHttpErrorSetup.Zip(new Action<Exception>[]
    {
            void (ex) => Assert.IsType<AzureSignalRRuntimeException>(ex),
            void (ex) => Assert.IsType<AzureSignalRInvalidArgumentException>(ex),
            void (ex) => Assert.IsType<AzureSignalRInaccessibleEndpointException>(ex),
            void (ex) => Assert.IsType<AzureSignalRUnauthorizedException>(ex),
            void (ex) => Assert.IsType<TaskCanceledException>(ex),
    })
    from api in MessageApis
    select new object[] { pair.First, pair.Second, api };

    [Theory]
    [MemberData(nameof(NotRetryable_RetryTest_MessageApiTestData))]
    public async Task NonRetryableError_RetryTestMessageApi(Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>> setup, Action<Exception> assert, Func<ServiceHubContext, Task> api)
    {
        await NonRetryableError_RetryTestCore(setup, assert, api, Constants.HttpClientNames.MessageResilient);
    }

    private static async Task NonRetryableError_RetryTestCore(Func<HttpHandlerSetup, Moq.Language.Flow.IReturnsResult<HttpMessageHandler>> setup, Action<Exception> assert, Func<ServiceHubContext, Task> testAction, string httpClientName)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        setup(handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()));

        var hubContext = await new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                o.HttpClientTimeout = TimeSpan.FromMilliseconds(1000);
                o.RetryOptions = new ServiceManagerRetryOptions
                {
                    Mode = ServiceManagerRetryMode.Fixed,
                    Delay = TimeSpan.FromMilliseconds(50),
                    MaxRetries = 3
                };
            })
            .ConfigureServices(services => services
                .AddHttpClient(httpClientName)
                .ConfigurePrimaryHttpMessageHandler(sp => handlerMock.Object))
            .BuildServiceManager()
            .CreateHubContextAsync(HubName, default);
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => testAction(hubContext));
        assert(exception);
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
                 o.RetryOptions = new ServiceManagerRetryOptions
                 {
                     Mode = ServiceManagerRetryMode.Fixed,
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
