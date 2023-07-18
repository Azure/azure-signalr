﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class DependencyInjectionExtensionFacts
    {
        private const string Url = "https://abc";
        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";
        private static readonly string TestConnectionString = $"Endpoint={Url};AccessKey={AccessKey};Version=1.0;";

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
        public async Task ProxyApplyToTransientModeTestAsync()
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
                o.ConnectionString = "Endpoint=http://abc;AccessKey=nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=;Version=1.0;";
                o.Proxy = new WebProxy(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First());
            }).BuildServiceManager();
            Assert.True(await serviceManager.IsServiceHealthy(default));
            Assert.Equal("/api/v1/health", requestUrls.Dequeue());

            using var hubContext = await serviceManager.CreateHubContextAsync("hub", default);
            Assert.True(await hubContext.ClientManager.UserExistsAsync("userId"));
            Assert.Equal("/api/hubs/hub/users/userId", requestUrls.Dequeue());
            await app.StopAsync();
        }

        [Fact]
        public async Task CustomizeHttpClientTimeoutTestAsync()
        {
            for (int i = 0; i < 10; i++)
            {
                using var serviceManager = new ServiceManagerBuilder()
                    .WithOptions(o =>
                    {
                        // use http schema to avoid SSL handshake
                        o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                        o.HttpClientTimeout = TimeSpan.FromSeconds(1);
                    })
                    .ConfigureServices(services => services.AddHttpClient(Options.DefaultName).AddHttpMessageHandler(sp => new WaitInfinitelyHandler()))
                    .BuildServiceManager();
                var requestStartTime = DateTime.UtcNow;
                var serviceHubContext = await serviceManager.CreateHubContextAsync("hub", default);
                await Assert.ThrowsAsync<TaskCanceledException>(() => serviceHubContext.Clients.All.SendCoreAsync("method", null));
                var elapsed = DateTime.UtcNow - requestStartTime;
                _outputHelper.WriteLine($"Request elapsed time: {elapsed.Ticks}");
                // Don't know why, the elapsed time sometimes is shorter than 1 second, but it should be close to 1 second.
                Assert.True(elapsed >= TimeSpan.FromSeconds(0.8));
                Assert.True(elapsed < TimeSpan.FromSeconds(1.2));
            }
        }

        private class WaitInfinitelyHandler : DelegatingHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                // Make the HTTP request pending forever until the token is cancelled.
                await Task.Delay(-1, cancellationToken);
                return null;
            }
        }
    }
}