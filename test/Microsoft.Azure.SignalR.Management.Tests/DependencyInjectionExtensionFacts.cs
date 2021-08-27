// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
            string configPath = nameof(DependencyInjectionExtensionFacts);
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
            ServiceCollection services = new ServiceCollection();
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
                                   .Configure<ServiceManagerOptions>(o => o.ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(2).ToArray())
                                   .BuildServiceProvider()
                                   .GetRequiredService<IOptions<ServiceManagerOptions>>()
                                   .Value);
        }

        [Fact]
        public async Task ServiceEndpoints_NotAppliedToTransientModeAsync()
        {
            // to avoid possible file name conflict with another FileConfigHotReloadTest
            string configPath = nameof(ServiceEndpoints_NotAppliedToTransientModeAsync);
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

            //update json config file
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
            Assert.Equal(connStr, optionsMonitor.CurrentValue.ConnectionString);// new config not reloaded
        }
    }
}