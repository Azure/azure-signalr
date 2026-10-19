// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using Newtonsoft.Json;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests;

public class DependencyInjectionExtensionFacts
{
    private const string Url = "https://abc";
    private const string AccessKey = "fake_key";
    private const string TestConnectionString = $"Endpoint={Url};AccessKey={AccessKey};Version=1.0;";

    private readonly ITestOutputHelper _outputHelper;

    public DependencyInjectionExtensionFacts(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Fact]
    public async Task FileConfigHotReloadTest()
    {
        // to avoid possible file name conflict with another FileConfigHotReloadTest
        var configPath = nameof(DependencyInjectionExtensionFacts);
        var originUrl = "http://origin.url";
        var newUrl = "http://new.url";
        var configObj = new
        {
            Azure = new
            {
                SignalR = new ServiceManagerOptions
                {
                    ConnectionString = $"Endpoint={originUrl};AccessKey={AccessKey};Version=1.0;"
                }
            }
        };
        File.WriteAllText(configPath, JsonConvert.SerializeObject(configObj));
        var services = new ServiceCollection();
        services.AddSignalRServiceManager();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddJsonFile(configPath, false, true).Build());
        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ServiceOptions>>();
        Assert.Equal(originUrl, new ServiceEndpoint(optionsMonitor.CurrentValue.ConnectionString).Endpoint);

        //update json config file
        configObj.Azure.SignalR.ConnectionString = $"Endpoint={newUrl};AccessKey={AccessKey};Version=1.0;";
        File.WriteAllText(configPath, JsonConvert.SerializeObject(configObj));

        await Task.Delay(5000);
        Assert.Equal(newUrl, new ServiceEndpoint(optionsMonitor.CurrentValue.ConnectionString).Endpoint);
    }

    [Fact]
    public void MemoryConfigHotReloadTest()
    {
        var originUrl = "http://origin.url";
        var newUrl = "http://new.url";
        var configProvider = new ReloadableMemoryProvider();
        configProvider.Set("Azure:SignalR:ConnectionString", $"Endpoint={originUrl};AccessKey={AccessKey};Version=1.0;");
        var services = new ServiceCollection()
            .AddSignalRServiceManager()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Add(new ReloadableMemorySource(configProvider)).Build());
        using var provider = services.BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ServiceOptions>>();
        Assert.Equal(originUrl, new ServiceEndpoint(optionsMonitor.CurrentValue.ConnectionString).Endpoint);

        //update
        configProvider.Set("Azure:SignalR:ConnectionString", $"Endpoint={newUrl};AccessKey={AccessKey};Version=1.0;");
        Assert.Equal(newUrl, new ServiceEndpoint(optionsMonitor.CurrentValue.ConnectionString).Endpoint);
    }

    [Fact]
    public void ProductInfoDefaultValueNotNullFact()
    {
        var services = new ServiceCollection()
            .AddSignalRServiceManager()
            .Configure<ServiceManagerOptions>(o =>
        {
            o.ConnectionString = TestConnectionString;
            o.ServiceTransportType = ServiceTransportType.Persistent;
        });
        using var serviceProvider = services.BuildServiceProvider();
        var productInfo = serviceProvider.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.ProductInfo;
        Assert.Matches("^Microsoft.Azure.SignalR.Management/", productInfo);
    }

    [Fact]
    public void ProductInfoFromCallingAssemblyFact()
    {
        var services = new ServiceCollection()
            .AddSignalRServiceManager()
            .Configure<ServiceManagerOptions>(o =>
            {
                o.ConnectionString = TestConnectionString;
                o.ServiceTransportType = ServiceTransportType.Persistent;
            });
        services.WithAssembly(Assembly.GetExecutingAssembly());
        using var serviceProvider = services.BuildServiceProvider();
        var productInfo = serviceProvider.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.ProductInfo;
        Assert.Matches("^Microsoft.Azure.SignalR.Management.Tests/", productInfo);
    }

    [Fact]
    public void AddUserAgent()
    {
        var services = new ServiceCollection()
            .AddSignalRServiceManager()
            .Configure<ServiceManagerOptions>(o =>
            {
                o.ConnectionString = TestConnectionString;
            })
            .AddUserAgent(" [key=value]");
        using var serviceProvider = services.BuildServiceProvider();
        var productInfo = serviceProvider.GetRequiredService<IOptions<ServiceManagerOptions>>().Value.ProductInfo;
        Assert.EndsWith(" [key=value]", productInfo);
    }

    [Fact]
    public void ConfigureByDelegateFact()
    {
        var services = new ServiceCollection()
            .AddSignalRServiceManager()
            .Configure<ServiceManagerOptions>(o =>
            {
                o.ConnectionString = TestConnectionString;
                o.ServiceTransportType = ServiceTransportType.Persistent;
            });
        using var serviceProvider = services.BuildServiceProvider();
        var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ServiceManagerOptions>>();
        Assert.Equal(Url, new ServiceEndpoint(optionsMonitor.CurrentValue.ConnectionString).Endpoint);
        Assert.Equal(ServiceTransportType.Persistent, optionsMonitor.CurrentValue.ServiceTransportType);
    }

    [Fact]
    public void ConfigureByFileAndDelegateFact()
    {
        var originUrl = "http://origin.url";
        var newUrl = "http://new.url";
        var appName = "AppName";
        var newAppName = "NewAppName";
        var configProvider = new ReloadableMemoryProvider();
        configProvider.Set("Azure:SignalR:ConnectionString", $"Endpoint={originUrl};AccessKey={AccessKey};Version=1.0;");
        var services = new ServiceCollection()
            .Configure<ServiceManagerOptions>(o =>
            {
                o.ApplicationName = appName;
            })
            .AddSignalRServiceManager()
        .AddSingleton<IConfiguration>(new ConfigurationBuilder().Add(new ReloadableMemorySource(configProvider)).Build());
        using var serviceProvider = services.BuildServiceProvider();
        var contextMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ServiceManagerOptions>>();
        Assert.Equal(appName, contextMonitor.CurrentValue.ApplicationName);

        configProvider.Set("Azure:SignalR:ConnectionString", $"Endpoint={newUrl};AccessKey={AccessKey};Version=1.0;");
        Assert.Equal(appName, contextMonitor.CurrentValue.ApplicationName);  // configuration via delegate is conserved after reload config.
        Assert.Equal(newUrl, new ServiceEndpoint(contextMonitor.CurrentValue.ConnectionString).Endpoint);

        configProvider.Set("Azure:SignalR:ApplicationName", newAppName);
        Assert.Equal(newAppName, contextMonitor.CurrentValue.ApplicationName);
    }

    [Fact]
    public void ConnectionStringNull_TransientMode_Throw()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddSignalRServiceManager()
                               .BuildServiceProvider()
                               .GetRequiredService<IOptions<ServiceManagerOptions>>()
                               .Value);
    }

    [Fact]
    public async Task MultiServiceEndpoints_NotAppliedToTransientModeAsync()
    {
        // to avoid possible file name conflict with another FileConfigHotReloadTest
        var configPath = nameof(MultiServiceEndpoints_NotAppliedToTransientModeAsync);
        var connStr = FakeEndpointUtils.GetFakeConnectionString(1).Single();
        var configObj = new
        {
            Azure = new
            {
                SignalR = new ServiceManagerOptions
                {
                    ConnectionString = connStr
                }
            }
        };
        File.WriteAllText(configPath, JsonConvert.SerializeObject(configObj));
        var provider = new ServiceCollection().AddSignalRServiceManager()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().AddJsonFile(configPath, false, true).Build())
            .BuildServiceProvider();
        var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ServiceOptions>>();
        Assert.Equal(connStr, optionsMonitor.CurrentValue.ConnectionString);

        //update json config file which won't pass validation
        var newConfigObj = new
        {
            Azure = new
            {
                SignalR = new
                {
                    Endpoints = new
                    {
                        First = FakeEndpointUtils.GetFakeConnectionString(1).Single()
                    },
                    ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single()
                }
            }
        };
        File.WriteAllText(configPath, JsonConvert.SerializeObject(newConfigObj));
        await Task.Delay(5000);
        Assert.Equal(connStr, optionsMonitor.CurrentValue.ConnectionString);// as new config don't pass validation, it is not reloaded
    }

    [Fact]
    public async Task ProxyApplyToUserRestRequestTestAsync()
    {
        var requestUrls = new Queue<string>();

        //create a simple proxy server
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.Services.AddLogging(b => b.AddXunit(_outputHelper));
        using var app = appBuilder.Build();
        //randomly choose a free port, listen to all interfaces
        app.Urls.Add("http://[::1]:0");
        app.Run(async context =>
        {
            requestUrls.Enqueue(context.Request.Path);
            await context.Response.WriteAsync("");
        });
        await app.StartAsync();

        var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            // use http schema to avoid SSL handshake
            o.ConnectionString = "Endpoint=http://abc;AccessKey=fake_key;Version=1.0;";
            o.Proxy = new WebProxy(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First());
        }).BuildServiceManager();
        Assert.True(await serviceManager.IsServiceHealthy(default));
        Assert.Equal("/api/health", requestUrls.Dequeue());

        using var hubContext = await serviceManager.CreateHubContextAsync("hub", default);
        Assert.True(await hubContext.ClientManager.UserExistsAsync("userId"));
        Assert.Equal("/api/hubs/hub/users/userId", requestUrls.Dequeue());
        await hubContext.Clients.All.SendAsync("method");
        Assert.Equal("/api/hubs/hub/:send", requestUrls.Dequeue());
        await app.StopAsync();
    }

    [Fact]
    public async Task ProxyAppliesToMicrosoftEntraNegotiationAsync()
    {
        var requestPaths = new Queue<string>();
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.Services.AddLogging(b => b.AddXunit(_outputHelper));
        using var app = appBuilder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.Run(async context =>
        {
            requestPaths.Enqueue(context.Request.Path);
            // Access-key refresh must not inherit the user REST client's short timeout.
            await Task.Delay(50);
            await context.Response.WriteAsync("{\"KeyId\":\"kid\",\"AccessKey\":\"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789\"}");
        });
        await app.StartAsync();

        var credential = new Mock<TokenCredential>();
        credential.Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<AccessToken>(new AccessToken("token", DateTimeOffset.UtcNow.AddHours(1))));
        using var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ServiceEndpoints = new[] { new ServiceEndpoint(new Uri("http://signalr.invalid"), credential.Object) };
            o.ServiceTransportType = ServiceTransportType.Transient;
            o.HttpClientTimeout = TimeSpan.FromMilliseconds(1);
            o.Proxy = new WebProxy(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single());
        }).BuildServiceManager();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var hubContext = await serviceManager.CreateHubContextAsync("hub", timeout.Token);
        var response = await hubContext.NegotiateAsync(cancellationToken: timeout.Token);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.Equal("/api/v1/auth/accessKey", Assert.Single(requestPaths));
        await app.StopAsync();
    }

    [Fact]
    public async Task ServiceManagersSharingEndpointUseTheirOwnProxiesAsync()
    {
        var requests = new ConcurrentQueue<(string Proxy, string Path)>();
        using var proxyA = CreateProxy("A");
        using var proxyB = CreateProxy("B");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await proxyA.StartAsync(timeout.Token);
        await proxyB.StartAsync(timeout.Token);

        var credential = new Mock<TokenCredential>();
        credential.Setup(c => c.GetTokenAsync(It.IsAny<TokenRequestContext>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<AccessToken>(new AccessToken("token", DateTimeOffset.UtcNow.AddHours(1))));
        var endpoint = new ServiceEndpoint(new Uri("http://signalr.invalid"), credential.Object);

        using var managerA = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ServiceEndpoints = new[] { endpoint };
            o.ServiceTransportType = ServiceTransportType.Transient;
            o.Proxy = new WebProxy(proxyA.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single());
        }).BuildServiceManager();
        using var managerB = new ServiceManagerBuilder().WithOptions(o =>
        {
            o.ServiceEndpoints = new[] { endpoint };
            o.ServiceTransportType = ServiceTransportType.Transient;
            o.Proxy = new WebProxy(proxyB.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single());
        }).BuildServiceManager();
        using var hubA = await managerA.CreateHubContextAsync("hub", timeout.Token);
        using var hubB = await managerB.CreateHubContextAsync("hub", timeout.Token);

        // B requests the first key, although A's manager and hub were created first.
        var responseB = await hubB.NegotiateAsync(cancellationToken: timeout.Token);
        Assert.False(string.IsNullOrEmpty(responseB.AccessToken));
        Assert.Equal(("B", "/api/v1/auth/accessKey"), Assert.Single(requests));

        // A owns a separate cache, so it fetches through A's proxy while B's key is still fresh.
        var responseA = await hubA.NegotiateAsync(cancellationToken: timeout.Token);
        Assert.False(string.IsNullOrEmpty(responseA.AccessToken));
        Assert.Equal(new[] { ("B", "/api/v1/auth/accessKey"), ("A", "/api/v1/auth/accessKey") }, requests.ToArray());

        // A second hub has a separate DI host, but shares this manager's key cache.
        using var secondHubA = await managerA.CreateHubContextAsync("secondHub", timeout.Token);
        var cachedResponseA = await secondHubA.NegotiateAsync(cancellationToken: timeout.Token);
        Assert.False(string.IsNullOrEmpty(cachedResponseA.AccessToken));
        Assert.Equal(2, requests.Count);

        await proxyA.StopAsync(timeout.Token);
        await proxyB.StopAsync(timeout.Token);

        WebApplication CreateProxy(string name)
        {
            var appBuilder = WebApplication.CreateBuilder();
            appBuilder.Services.AddLogging(b => b.AddXunit(_outputHelper));
            var app = appBuilder.Build();
            app.Urls.Add("http://127.0.0.1:0");
            app.Run(async context =>
            {
                requests.Enqueue((name, context.Request.Path));
                await context.Response.WriteAsync("{\"KeyId\":\"kid\",\"AccessKey\":\"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789\"}");
            });
            return app;
        }
    }

    [Fact]
    public async Task ProxyApplyToInternalHealthCheckTestAsync()
    {
        var requestUrls = new Queue<string>();

        //create a simple proxy server
        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.Services.AddLogging(b => b.AddXunit(_outputHelper));
        using var app = appBuilder.Build();
        //randomly choose a free port, listen to all interfaces
        app.Urls.Add("http://[::1]:0");
        app.Run(context =>
        {
            if (HttpMethods.IsHead(context.Request.Method))
            {
                requestUrls.Enqueue(context.Request.Path);
            }
            return Task.CompletedTask;
        });
        await app.StartAsync();

        var serviceManager = new ServiceManagerBuilder().WithOptions(o =>
        {
            // use http schema to avoid SSL handshake
            o.ConnectionString = "Endpoint=http://abc;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Version=1.0;";
            o.Proxy = new WebProxy(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First());
            o.ServiceTransportType = ServiceTransportType.Transient;
        }).ConfigureServices(services => services.Configure<HealthCheckOption>(o => o.EnabledForSingleEndpoint = true)).BuildServiceManager();

        using var hubContext = await serviceManager.CreateHubContextAsync("hub", default);
        Assert.Equal("/api/health", requestUrls.Dequeue());
        await app.StopAsync();
    }

    public static IEnumerable<object[]> CustomizeHttpClientTimeoutTestData => new object[][]
    {
        new object[]{ Constants.HttpClientNames.MessageResilient , (ServiceHubContext serviceHubContext)=> serviceHubContext.Clients.All.SendCoreAsync("method", null) },
        new object[]{ Constants.HttpClientNames.Resilient , (ServiceHubContext serviceHubContext)=> serviceHubContext.ClientManager.CloseConnectionAsync("connectionId") },
        new object[]{Constants.HttpClientNames.UserDefault, (ServiceHubContext serviceHubContext)=>  (serviceHubContext as ServiceHubContextImpl).ServiceProvider.GetRequiredService<IServiceManager>().IsServiceHealthy(default)}
    };

    [Theory]
    [MemberData(nameof(CustomizeHttpClientTimeoutTestData))]
    public async Task CustomizeHttpClientTimeoutTestAsync(string httpClientName, Func<ServiceHubContext, Task> testFunc)
    {
        for (var i = 0; i < 10; i++)
        {
            using var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    // use http schema to avoid SSL handshake
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                    o.HttpClientTimeout = TimeSpan.FromSeconds(1);
                })
                .ConfigureServices(services => services.AddHttpClient(httpClientName).AddHttpMessageHandler(sp => new WaitInfinitelyHandler()))
                .BuildServiceManager();
            using var serviceHubContext = await serviceManager.CreateHubContextAsync("hub", default);
            var httpClientFactory = ((ServiceHubContextImpl)serviceHubContext).ServiceProvider.GetRequiredService<IHttpClientFactory>();
            using var httpClient = httpClientFactory.CreateClient(httpClientName);
            Assert.Equal(TimeSpan.FromSeconds(1), httpClient.Timeout);

            // Verify timeout cancellation independently of the runner's scheduling delays.
            var exception = await Assert.ThrowsAsync<TaskCanceledException>(() => testFunc(serviceHubContext)).OrTimeout(TimeSpan.FromSeconds(30));
            Assert.IsType<TimeoutException>(exception.InnerException);
        }
    }

    [Theory]
    [InlineData(Constants.HttpClientNames.InternalDefault)]
    [InlineData(Constants.HttpClientNames.UserDefault)]
    [InlineData(Constants.HttpClientNames.MessageResilient)]
    [InlineData(Constants.HttpClientNames.Resilient)]
    public async Task HttpClientProductInfoTestAsync(string httpClientName)
    {
        using var hubContext = await new ServiceManagerBuilder()
            .WithOptions(o => o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single())
            .ConfigureServices(services => services.AddHttpClient(httpClientName)
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        new TestRootHandler((message, token) =>
                        {
                            if (message.Headers.TryGetValues(Constants.AsrsUserAgent, out var values))
                            {
                                Assert.Single(values);
                                Assert.Matches("^Microsoft.Azure.SignalR.Management/", values.Single());
                            }
                            else
                            {
                                throw new InvalidOperationException("Product info header is missing");
                            }
                        })))
            .BuildServiceManager()
            .CreateHubContextAsync("hubName", default);
        var serviceProvider = (hubContext as ServiceHubContextImpl).ServiceProvider;
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(httpClientName);
        await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://abc"));
    }

    [Theory]
    [InlineData(Constants.HttpClientNames.Resilient)]
    [InlineData(Constants.HttpClientNames.MessageResilient)]
    public async Task HttpClientMessageTracingIdEnabledTestAsync(string httpClientName)
    {
        using var hubContext = await new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                o.EnableMessageTracing = true;
            })
            .ConfigureServices(services => services.AddHttpClient(httpClientName)
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        new TestRootHandler((message, token) =>
                        {
                            if (message.Headers.TryGetValues(Constants.Headers.AsrsMessageTracingId, out var values))
                            {
                                Assert.Single(values);
                                Convert.ToUInt64(values.Single(), CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                throw new InvalidOperationException("Message tracing Id header is missing");
                            }
                        })))
            .BuildServiceManager()
            .CreateHubContextAsync("hubName", default);
        var serviceProvider = (hubContext as ServiceHubContextImpl).ServiceProvider;
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(httpClientName);
        await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://abc"));
    }

    [Theory]
    [InlineData(Constants.HttpClientNames.Resilient)]
    [InlineData(Constants.HttpClientNames.MessageResilient)]
    public async Task HttpClientMessageTracingIdDisabledTestAsync(string httpClientName)
    {
        using var hubContext = await new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                o.EnableMessageTracing = false;
            })
            .ConfigureServices(services => services.AddHttpClient(httpClientName)
                        .ConfigurePrimaryHttpMessageHandler(() =>
                        new TestRootHandler((message, token) =>
                        {
                            if (message.Headers.TryGetValues(Constants.Headers.AsrsMessageTracingId, out var values))
                            {
                                throw new InvalidOperationException("Message tracing Id header is not expected");
                            }
                        })))
            .BuildServiceManager()
            .CreateHubContextAsync("hubName", default);
        var serviceProvider = (hubContext as ServiceHubContextImpl).ServiceProvider;
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        using var httpClient = httpClientFactory.CreateClient(httpClientName);
        await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://abc"));
    }

    [Fact]
    public void AddHub_HubNameNull_Throw()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ServiceCollection().AddHub<Hub>(null));
    }

    [Theory]
    [InlineData(Constants.HttpClientNames.InternalDefault)]
    [InlineData(Constants.HttpClientNames.UserDefault)]
    [InlineData(Constants.HttpClientNames.MessageResilient)]
    [InlineData(Constants.HttpClientNames.Resilient)]
    public async Task AccessTokenHttpMessageHandlerAdded(string httpClientName)
    {
        var serviceProvider = new ServiceCollection()
            .AddSignalRServiceManager()
            .Configure<ServiceManagerOptions>(o =>
            {
                o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
            })
            .AddHttpClient(httpClientName)
            .ConfigurePrimaryHttpMessageHandler(sp=>new TestRootHandler((message, cancellationToken) =>
            {
                Assert.NotNull(message.Headers.Authorization);
            } ))
            .Services
            .BuildServiceProvider();
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
        var client = httpClientFactory.CreateClient(httpClientName);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://abc/api/test?key=value");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class WaitInfinitelyHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Make the HTTP request pending forever until the token is cancelled.
            await Task.Delay(-1, cancellationToken);
            return null;
        }
    }
}
