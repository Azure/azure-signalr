// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Microsoft.Azure.SignalR.Management.Tests;

public class ServiceManagerFacts
{
    private const string Endpoint = "https://abc";

    private const string AccessKey = "fake_key";

    private const string HubName = "signalrBench";

    private const string UserId = "UserA";

    private const string TestConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";

    private static readonly TimeSpan TokenLifeTime = TimeSpan.FromSeconds(99);

    private static readonly Claim[] DefaultClaims = [new("type1", "val1")];

    private static readonly ServiceTransportType[] ServiceTransportTypes = [ServiceTransportType.Transient, ServiceTransportType.Persistent];

    private static readonly bool[] UseLoggerFatories = [false, true];

    private static readonly string[] AppNames = ["appName", "", null];

    private static readonly string[] UserIds = [UserId, null];

    private static readonly IEnumerable<Claim[]> ClaimLists = [DefaultClaims, null];

    private static readonly int[] ConnectionCounts = [1, 2];

    public static IEnumerable<object[]> TestServiceManagerOptionData => from transport in ServiceTransportTypes
                                                                        from useLoggerFactory in UseLoggerFatories
                                                                        from appName in AppNames
                                                                        from connectionCount in ConnectionCounts
                                                                        select new object[] { transport, useLoggerFactory, appName, connectionCount };

    public static IEnumerable<object[]> TestGenerateClientEndpointData => from appName in AppNames
                                                                          select new object[] { appName, ClientEndpointUtils.GetExpectedClientEndpoint(HubName, appName) };

    public static IEnumerable<object[]> TestGenerateAccessTokenData => from userId in UserIds
                                                                       from claims in ClaimLists
                                                                       from appName in AppNames
                                                                       select new object[] { userId, claims, appName };

    [Fact]
    public void DisposeTest()
    {
        var serviceManager = new ServiceManagerBuilder().WithOptions(o => o.ConnectionString = TestConnectionString).Build();
        serviceManager.Dispose();
    }

    [Fact]
    public void ConnectionStringAbsent_Throw_Test()
    {
        Assert.Throws<InvalidOperationException>(() => new ServiceManagerBuilder().Build());
    }

    [Fact]
    public async Task TestCreateServiceHubContext()
    {
        using var serviceHubContext = await new ServiceManagerBuilder()
            .WithOptions(o => o.ConnectionString = TestConnectionString)
            // avoid waiting for health check result for long time
            .ConfigureServices(ConfigureTestHttpClient(HttpStatusCode.OK))
            .BuildServiceManager()
            .CreateHubContextAsync(HubName, default);
        Assert.Equal(1, (serviceHubContext as ServiceHubContextImpl).ServiceProvider.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.ConnectionCount);
    }

    [Fact]
    public async Task TestConnectionCountCustomizable()
    {
        using var serviceHubContext = await new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = TestConnectionString;
                o.ConnectionCount = 5;
            })
            // avoid waiting for health check result for long time
            .ConfigureServices(ConfigureTestHttpClient(HttpStatusCode.OK))
            .BuildServiceManager()
            .CreateHubContextAsync(HubName, default);
        Assert.Equal(5, (serviceHubContext as ServiceHubContextImpl).ServiceProvider.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.ConnectionCount);
    }

    [Theory]
    [MemberData(nameof(TestGenerateAccessTokenData))]
    internal void GenerateClientAccessTokenTest(string userId, Claim[] claims, string appName)
    {
        var builder = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ApplicationName = appName;
                o.ConnectionString = TestConnectionString;
            });
        var manager = builder.Build();
        var tokenString = manager.GenerateClientAccessToken(HubName, userId, claims, TokenLifeTime);
        var token = JwtTokenHelper.JwtHandler.ReadJwtToken(tokenString);

        var expectedToken = JwtTokenHelper.GenerateExpectedAccessToken(token, ClientEndpointUtils.GetExpectedClientEndpoint(HubName, appName), AccessKey, claims);

        Assert.Equal(expectedToken, tokenString);
    }

    [Theory]
    [MemberData(nameof(TestGenerateClientEndpointData))]
    internal void GenerateClientEndpointTest(string appName, string expectedClientEndpoint)
    {
        var builder = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ApplicationName = appName;
                o.ConnectionString = TestConnectionString;
            });
        var manager = builder.Build();
        var clientEndpoint = manager.GetClientEndpoint(HubName);

        Assert.Equal(expectedClientEndpoint, clientEndpoint);
    }

    [Fact]
    internal void GenerateClientEndpointTestWithClientEndpoint()
    {
        var manager = new ServiceManagerBuilder().WithOptions(o => o.ConnectionString = $"Endpoint=http://localhost;AccessKey=ABC;Version=1.0;ClientEndpoint=https://remote").Build();
        var clientEndpoint = manager.GetClientEndpoint(HubName);

        Assert.Equal("https://remote/client/?hub=signalrbench", clientEndpoint);
    }

    [Theory(Skip = "Reenable when it is ready")]
    [MemberData(nameof(TestServiceManagerOptionData))]
    internal async Task CreateServiceHubContextTest(ServiceTransportType serviceTransportType, bool useLoggerFacory, string appName, int connectionCount)
    {
        var builder = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ServiceTransportType = serviceTransportType;
                o.ApplicationName = appName;
                o.ConnectionCount = connectionCount;
                o.ConnectionString = TestConnectionString;
            });
        var serviceManager = builder.Build();

        using var loggerFactory = useLoggerFacory ? (ILoggerFactory)new LoggerFactory() : NullLoggerFactory.Instance;
        var hubContext = await serviceManager.CreateHubContextAsync(HubName, default);
    }

    [Fact]
    internal async Task IsServiceHealthy_ReturnTrue_Test()
    {
        var services = new ServiceCollection()
            .AddSignalRServiceManager()
            .Configure<ServiceManagerOptions>(o => o.ConnectionString = TestConnectionString);
        ConfigureTestHttpClient(HttpStatusCode.OK)(services);
        var serviceManager = services.AddSingleton(services.ToList() as IReadOnlyCollection<ServiceDescriptor>)
            .BuildServiceProvider()
            .GetRequiredService<IServiceManager>();

        var actual = await serviceManager.IsServiceHealthy(default);

        Assert.True(actual);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    internal async Task IsServiceHealthy_ReturnFalse_Test(HttpStatusCode statusCode)
    {
        var services = new ServiceCollection();
        services.Configure<ServiceManagerOptions>(o => o.ConnectionString = TestConnectionString);
        services.AddSignalRServiceManager();
        ConfigureTestHttpClient(statusCode)(services);
        services.AddSingleton(services.ToList() as IReadOnlyCollection<ServiceDescriptor>);
        using var serviceManager = services.BuildServiceProvider().GetRequiredService<IServiceManager>();

        var actual = await serviceManager.IsServiceHealthy(default);

        Assert.False(actual);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, typeof(AzureSignalRInvalidArgumentException))]
    [InlineData(HttpStatusCode.Unauthorized, typeof(AzureSignalRUnauthorizedException))]
    [InlineData(HttpStatusCode.NotFound, typeof(AzureSignalRInaccessibleEndpointException))]
    [InlineData(HttpStatusCode.Ambiguous, typeof(AzureSignalRRuntimeException))]
    internal async Task IsServiceHealthy_Throw_Test(HttpStatusCode statusCode, Type expectedException)
    {
        var services = new ServiceCollection();
        services.AddSignalRServiceManager();
        services.Configure<ServiceManagerOptions>(o => o.ConnectionString = TestConnectionString);
        ConfigureTestHttpClient(statusCode)(services);
        services.AddSingleton(services.ToList() as IReadOnlyCollection<ServiceDescriptor>);
        using var serviceManager = services.BuildServiceProvider().GetRequiredService<IServiceManager>();

        var exception = await Assert.ThrowsAnyAsync<AzureSignalRException>(() => serviceManager.IsServiceHealthy(default));
        Assert.IsType(expectedException, exception);
    }

    private static Action<IServiceCollection> ConfigureTestHttpClient(HttpStatusCode statusCode)
    {
        return services => services.AddHttpClient(Constants.HttpClientNames.UserDefault)
            .ConfigurePrimaryHttpMessageHandler(() => new TestRootHandler(statusCode));
    }
}
