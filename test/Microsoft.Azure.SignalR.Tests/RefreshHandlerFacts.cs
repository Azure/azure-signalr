// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Azure;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class RefreshHandlerFacts
{
    private const string ConnectionString = "Endpoint=http://localhost;Port=8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ProcessAsync_RuntimeSuccess_ForwardsRequestAndReturnsTokenWithRuntimeClaims(bool configureCallback)
    {
        const string connectionToken = "refresh-connection-token";
        var expiration = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var serviceEndpoint = new ServiceEndpoint(ConnectionString.Replace("localhost", "owning-endpoint", StringComparison.Ordinal));
        var owningEndpoint = new HubServiceEndpoint(
            nameof(TestHub), new ServiceEndpointProvider(serviceEndpoint, new ServiceOptions()), serviceEndpoint);
        var manager = new FakeServiceConnectionManager<TestHub>
        {
            ClaimsResult = new GetConnectionClaimsResult(
                AckStatus.Ok, new[] { new Claim("tenant", "previous-tenant") }, owningEndpoint),
            RefreshResult = new RefreshAuthResult(AckStatus.Ok, new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "alice"),
                new Claim("tenant", "runtime-tenant"),
            }, owningEndpoint),
        };
        AuthenticationRefreshContext captured = null;
        await using var fixture = await CreateFixtureAsync(
            callback: configureCallback ? refreshContext =>
            {
                captured = refreshContext;
                return Task.FromResult(true);
            } : null,
            manager);
        using var cancellation = new CancellationTokenSource();
        var context = CreateContext();
        context.Request.QueryString = new QueryString($"?id={connectionToken}");
        context.RequestAborted = cancellation.Token;
        context.Features.Set(Mock.Of<IAuthenticateResultFeature>(feature =>
            feature.AuthenticateResult == AuthenticateResult.Success(new AuthenticationTicket(
                context.User, new AuthenticationProperties { ExpiresUtc = expiration }, "TestAuth"))));

        await fixture.Handler.ProcessAsync(context);

        Assert.Equal(1, manager.RefreshCallCount);
        Assert.NotNull(manager.RefreshMessage);
        Assert.Equal(connectionToken, manager.RefreshMessage.ConnectionToken);
        Assert.Equal(expiration, manager.RefreshMessage.ExpireTime);
        Assert.Equal(cancellation.Token, manager.RefreshCancellationToken);
        Assert.NotNull(manager.RefreshMessage.Claims);
        Assert.Equal(4, manager.RefreshMessage.Claims.Length);
        Assert.Equal("alice", Assert.Single(manager.RefreshMessage.Claims, claim => claim.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("alice", Assert.Single(manager.RefreshMessage.Claims, claim => claim.Type == Constants.ClaimType.UserId).Value);
        Assert.Equal("TestAuth", Assert.Single(manager.RefreshMessage.Claims, claim => claim.Type == Constants.ClaimType.AuthenticationType).Value);
        Assert.Equal("fabrikam", Assert.Single(manager.RefreshMessage.Claims, claim => claim.Type == "tenant").Value);
        if (configureCallback)
        {
            Assert.Equal(1, manager.GetClaimsCallCount);
            Assert.NotNull(manager.GetClaimsMessage);
            Assert.Equal(connectionToken, manager.GetClaimsMessage.ConnectionToken);
            Assert.Equal(cancellation.Token, manager.GetClaimsCancellationToken);
            Assert.Same(owningEndpoint, manager.PreferredEndpoint);
            Assert.NotNull(captured);
            Assert.Same(context, captured.HttpContext);
            Assert.Same(context.User, captured.NewUser);
            Assert.Equal(expiration, captured.NewExpiration);
            Assert.Equal("previous-tenant", Assert.Single(captured.PreviousUser.FindAll("tenant")).Value);
        }
        else
        {
            Assert.Equal(0, manager.GetClaimsCallCount);
            Assert.Null(manager.GetClaimsMessage);
            Assert.Null(manager.PreferredEndpoint);
            Assert.Null(captured);
        }
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var accessToken = document.RootElement.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrEmpty(accessToken));
        Assert.Equal((int)Constants.Periods.DefaultAccessTokenLifetime.TotalSeconds,
            document.RootElement.GetProperty("tokenLifetimeSeconds").GetInt32());
        var token = JwtTokenHelper.JwtHandler.ReadJwtToken(accessToken);
        Assert.Equal("runtime-tenant", Assert.Single(token.Claims, claim => claim.Type == "tenant").Value);
        Assert.Equal("alice", Assert.Single(token.Claims, claim => claim.Type == "nameid").Value);
        Assert.Equal("owning-endpoint", new Uri(Assert.Single(token.Audiences)).Host);
    }

    [Fact]
    public async Task ProcessAsync_CallbackReceivesFilteredPreviousUserAndNullExpiration()
    {
        AuthenticationRefreshContext captured = null;
        var manager = new FakeServiceConnectionManager<TestHub>
        {
            ClaimsResult = new GetConnectionClaimsResult(AckStatus.Ok, new[]
            {
                new Claim(Constants.ClaimType.AuthenticationType, "CustomAuth"),
                new Claim(Constants.ClaimType.AzureSignalRUserPrefix + "exp", "application-expiration"),
                new Claim("exp", "service-expiration"),
                new Claim("iat", "service-issued-at"),
                new Claim(Constants.ClaimType.AuthExpiresOn, "12345"),
                new Claim("tenant", "contoso"),
            }),
        };
        await using var fixture = await CreateFixtureAsync(
            callback: context =>
            {
                captured = context;
                return Task.FromResult(false);
            },
            manager);
        var context = CreateContext();

        await fixture.Handler.ProcessAsync(context);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.NotNull(captured);
        Assert.Null(captured.NewExpiration);
        Assert.Equal("application-expiration", Assert.Single(captured.PreviousUser.FindAll("exp")).Value);
        Assert.Equal("contoso", captured.PreviousUser.FindFirst("tenant")?.Value);
        Assert.DoesNotContain(captured.PreviousUser.Claims, claim => claim.Type is "iat" || claim.Type.StartsWith(Constants.ClaimType.AzureSignalRSysPrefix, StringComparison.Ordinal));
        Assert.Equal(1, manager.GetClaimsCallCount);
        Assert.Equal(0, manager.RefreshCallCount);
    }

    [Fact]
    public async Task ProcessAsync_CallbackThrows_ReturnsControlledInternalErrorWithoutRefreshing()
    {
        var manager = new FakeServiceConnectionManager<TestHub>
        {
            ClaimsResult = new GetConnectionClaimsResult(AckStatus.Ok, Array.Empty<Claim>()),
        };
        await using var fixture = await CreateFixtureAsync(
            callback: _ => throw new InvalidOperationException("Test callback failure."),
            manager);
        var context = CreateContext();

        await fixture.Handler.ProcessAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("internal_server_error", await ReadErrorAsync(context));
        Assert.Equal(1, manager.GetClaimsCallCount);
        Assert.Equal(0, manager.RefreshCallCount);
    }

    [Theory]
    [InlineData((int)AckStatus.NotFound, StatusCodes.Status404NotFound, "connection_not_found")]
    [InlineData((int)AckStatus.InternalServerError, StatusCodes.Status500InternalServerError, "internal_server_error")]
    public async Task ProcessAsync_ClaimsLookupFailure_ReturnsMappedErrorWithoutRefreshing(int status, int expectedStatusCode, string expectedError)
    {
        var manager = new FakeServiceConnectionManager<TestHub>
        {
            ClaimsResult = new GetConnectionClaimsResult((AckStatus)status),
        };
        await using var fixture = await CreateFixtureAsync(
            callback: _ => Task.FromResult(true),
            manager);
        var context = CreateContext();

        await fixture.Handler.ProcessAsync(context);

        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
        Assert.Equal(expectedError, await ReadErrorAsync(context));
        Assert.Equal(1, manager.GetClaimsCallCount);
        Assert.Equal(0, manager.RefreshCallCount);
    }

    [Theory]
    [InlineData((int)AckStatus.NotFound, StatusCodes.Status404NotFound, "connection_not_found")]
    [InlineData((int)AckStatus.Forbidden, StatusCodes.Status403Forbidden, "permission_change_rejected")]
    [InlineData((int)AckStatus.InternalServerError, StatusCodes.Status500InternalServerError, "internal_server_error")]
    public async Task ProcessAsync_RuntimeFailure_ReturnsMappedError(int status, int expectedStatusCode, string expectedError)
    {
        var manager = new FakeServiceConnectionManager<TestHub>
        {
            RefreshResult = new RefreshAuthResult((AckStatus)status),
        };
        await using var fixture = await CreateFixtureAsync(manager: manager);
        var context = CreateContext();

        await fixture.Handler.ProcessAsync(context);

        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
        Assert.Equal(expectedError, await ReadErrorAsync(context));
        Assert.Equal(1, manager.RefreshCallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ProcessAsync_IncompleteSuccessPayload_ReturnsInternalError(int payloadMode)
    {
        var manager = new FakeServiceConnectionManager<TestHub>();
        await using var fixture = await CreateFixtureAsync(manager: manager);
        manager.RefreshResult = payloadMode switch
        {
            0 => new RefreshAuthResult(AckStatus.Ok, claims: null, fixture.Endpoint),
            1 => new RefreshAuthResult(AckStatus.Ok, Array.Empty<Claim>(), fixture.Endpoint),
            _ => new RefreshAuthResult(AckStatus.Ok, new[] { new Claim("tenant", "contoso") }, owningEndpoint: null),
        };
        var context = CreateContext();

        await fixture.Handler.ProcessAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("internal_server_error", await ReadErrorAsync(context));
    }

    [Fact]
    public async Task ProcessAsync_TokenGenerationThrows_ReturnsInternalError()
    {
        var manager = new FakeServiceConnectionManager<TestHub>();
        var provider = new Mock<IServiceEndpointProvider>();
        provider
            .Setup(instance => instance.GenerateClientAccessTokenAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<Claim>>(),
                It.IsAny<TimeSpan?>()))
            .ThrowsAsync(new InvalidOperationException("Test token generation failure."));
        var serviceEndpoint = new ServiceEndpoint(ConnectionString);
        var failingEndpoint = new HubServiceEndpoint(nameof(TestHub), provider.Object, serviceEndpoint);
        var endpointManager = new Mock<IServiceEndpointManager>();
        endpointManager.SetupGet(instance => instance.Endpoints).Returns(
            new Dictionary<ServiceEndpoint, ServiceEndpoint> { [serviceEndpoint] = serviceEndpoint });
        endpointManager.Setup(instance => instance.GetEndpoints(nameof(TestHub))).Returns(new[] { failingEndpoint });
        endpointManager.Setup(instance => instance.GetEndpointProvider(serviceEndpoint)).Returns(provider.Object);
        await using var fixture = await CreateFixtureAsync(
            manager: manager,
            endpointManager: endpointManager.Object);
        manager.RefreshResult = new RefreshAuthResult(
            AckStatus.Ok,
            new[] { new Claim("tenant", "contoso") },
            failingEndpoint);
        var context = CreateContext();

        await fixture.Handler.ProcessAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("internal_server_error", await ReadErrorAsync(context));
        Assert.Equal(1, manager.RefreshCallCount);
        provider.Verify(instance => instance.GenerateClientAccessTokenAsync(
            nameof(TestHub),
            It.Is<IEnumerable<Claim>>(claims =>
                claims.Count() == 1
                && claims.Single().Type == "tenant"
                && claims.Single().Value == "contoso"),
            Constants.Periods.DefaultAccessTokenLifetime), Times.Once);
    }

    private static async Task<Fixture> CreateFixtureAsync(
        Func<AuthenticationRefreshContext, Task<bool>> callback = null,
        FakeServiceConnectionManager<TestHub> manager = null,
        IServiceEndpointManager endpointManager = null)
    {
        manager ??= new FakeServiceConnectionManager<TestHub>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSignalR().AddAzureSignalR(options => options.ConnectionString = ConnectionString);
        builder.Services.AddSingleton<IServiceConnectionManager<TestHub>>(manager);
        builder.Services.AddSingleton<IServiceConnectionFactory>(new TestServiceConnectionFactory());
        if (endpointManager is not null)
        {
            builder.Services.AddSingleton(endpointManager);
        }
        var app = builder.Build();
        app.MapHub<TestHub>("/hub", options =>
        {
            options.EnableAuthenticationRefresh = true;
            options.OnAuthenticationRefresh = callback;
        });
        await app.StartAsync();
        var endpoint = app.Services.GetRequiredService<IServiceEndpointManager>().GetEndpoints(nameof(TestHub)).Single();
        return new Fixture(app, app.Services.GetRequiredService<RefreshHandler<TestHub>>(), manager, endpoint);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.QueryString = new QueryString("?id=connection-token");
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "alice"), new Claim("tenant", "fabrikam") },
            "TestAuth"));
        return context;
    }

    private static async Task<string> ReadErrorAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("error").GetString();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public Fixture(
            WebApplication app,
            RefreshHandler<TestHub> handler,
            FakeServiceConnectionManager<TestHub> manager,
            HubServiceEndpoint endpoint)
        {
            _app = app;
            Handler = handler;
            Manager = manager;
            Endpoint = endpoint;
        }

        public RefreshHandler<TestHub> Handler { get; }

        public FakeServiceConnectionManager<TestHub> Manager { get; }

        public HubServiceEndpoint Endpoint { get; }

        public ValueTask DisposeAsync() => _app.DisposeAsync();
    }

    private sealed class TestHub : Hub
    {
    }

    private sealed class FakeServiceConnectionManager<THub> : IServiceConnectionManager<THub> where THub : Hub
    {
        public RefreshAuthResult RefreshResult { get; set; } = new(AckStatus.InternalServerError);

        public GetConnectionClaimsResult ClaimsResult { get; set; } = new(AckStatus.Ok, Array.Empty<Claim>());

        public int RefreshCallCount { get; private set; }

        public int GetClaimsCallCount { get; private set; }

        public RefreshAuthMessage RefreshMessage { get; private set; }

        public CancellationToken RefreshCancellationToken { get; private set; }

        public HubServiceEndpoint PreferredEndpoint { get; private set; }

        public GetConnectionClaimsMessage GetClaimsMessage { get; private set; }

        public CancellationToken GetClaimsCancellationToken { get; private set; }

        public void SetServiceConnection(IServiceConnectionContainer serviceConnection)
        {
        }

        public Task StartAsync() => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task OfflineAsync(GracefulShutdownMode mode, CancellationToken token) => Task.CompletedTask;

        public Task CloseClientConnections(CancellationToken token) => Task.CompletedTask;

        public Task WriteAsync(ServiceMessage serviceMessage) => Task.CompletedTask;

        public Task<bool> WriteAckableMessageAsync(ServiceMessage serviceMessage, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<RefreshAuthResult> RefreshAuthAsync(RefreshAuthMessage message, HubServiceEndpoint preferredEndpoint = null, CancellationToken cancellationToken = default)
        {
            RefreshCallCount++;
            RefreshMessage = message;
            PreferredEndpoint = preferredEndpoint;
            RefreshCancellationToken = cancellationToken;
            return Task.FromResult(RefreshResult);
        }

        public Task<GetConnectionClaimsResult> GetConnectionClaimsAsync(GetConnectionClaimsMessage message, CancellationToken cancellationToken = default)
        {
            GetClaimsCallCount++;
            GetClaimsMessage = message;
            GetClaimsCancellationToken = cancellationToken;
            return Task.FromResult(ClaimsResult);
        }

        public IAsyncEnumerable<Page<SignalRGroupMember>> ListConnectionsInGroupAsync(string groupName, int? top = null, int? maxPageSize = null, string continuationToken = null, ulong? tracingId = null, CancellationToken token = default) => throw new NotSupportedException();
    }
}
#endif
