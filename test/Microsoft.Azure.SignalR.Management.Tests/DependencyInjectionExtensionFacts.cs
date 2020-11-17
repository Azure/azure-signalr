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
            var originUrl = "http://originUrl";
            var newUrl = "http://newUrl";
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
            Assert.Equal(originUrl, optionsMonitor.CurrentValue.Endpoints.Single().Endpoint);

            //update json config file
            configObj.Azure.SignalR.ConnectionString = $"Endpoint={newUrl};AccessKey={AccessKey};Version=1.0;";
            File.WriteAllText(configPath, JsonConvert.SerializeObject(configObj));

            await Task.Delay(5000);
            Assert.Equal(newUrl, optionsMonitor.CurrentValue.Endpoints.Single().Endpoint);
        }

        [Fact]
        public void MemoryConfigHotReloadTest()
        {
            var originUrl = "http://originUrl";
            var newUrl = "http://newUrl";
            var configProvider = new ReloadableMemoryProvider();
            configProvider.Set("Azure:SignalR:ConnectionString", $"Endpoint={originUrl};AccessKey={AccessKey};Version=1.0;");
            var services = new ServiceCollection()
                .AddSignalRServiceManager()
                .AddSingleton<IConfiguration>(new ConfigurationBuilder().Add(new ReloadableMemorySource(configProvider)).Build());
            using var provider = services.BuildServiceProvider();
            var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ServiceOptions>>();
            Assert.Equal(originUrl, optionsMonitor.CurrentValue.Endpoints.Single().Endpoint);

            //update
            configProvider.Set("Azure:SignalR:ConnectionString", $"Endpoint={newUrl};AccessKey={AccessKey};Version=1.0;");
            Assert.Equal(newUrl, optionsMonitor.CurrentValue.Endpoints.Single().Endpoint);
        }

        [Fact]
        public void ProductInfoDefaultValueNotNullFact()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSignalRServiceManager(o =>
            {
                o.ConnectionString = TestConnectionString;
                o.ServiceTransportType = ServiceTransportType.Persistent;
            });
            using var serviceProvider = services.BuildServiceProvider();
            var productInfo = serviceProvider.GetRequiredService<IOptions<ServiceManagerContext>>().Value.ProductInfo;
            Assert.Matches("^Microsoft.Azure.SignalR.Management/", productInfo);
        }

        [Fact]
        public void ProductInfoFromCallingAssemblyFact()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSignalRServiceManager(o =>
            {
                o.ConnectionString = TestConnectionString;
                o.ServiceTransportType = ServiceTransportType.Persistent;
            });
            services.WithAssembly(Assembly.GetExecutingAssembly());
            using var serviceProvider = services.BuildServiceProvider();
            var productInfo = serviceProvider.GetRequiredService<IOptions<ServiceManagerContext>>().Value.ProductInfo;
            Assert.Matches("^Microsoft.Azure.SignalR.Management.Tests/", productInfo);
        }

        [Fact]
        public void ConfigureByDelegateFact()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddSignalRServiceManager(o =>
            {
                o.ConnectionString = TestConnectionString;
                o.ServiceTransportType = ServiceTransportType.Persistent;
            });
            using var serviceProvider = services.BuildServiceProvider();
            var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ServiceManagerContext>>();
            Assert.Equal(Url, optionsMonitor.CurrentValue.ServiceEndpoints.Single().Endpoint);
            Assert.Equal(ServiceTransportType.Persistent, optionsMonitor.CurrentValue.ServiceTransportType);
        }

        [Fact]
        public void ConfigureByFileAndDelegateFact()
        {
            var originUrl = "http://originUrl";
            var newUrl = "http://newUrl";
            var appName = "AppName";
            var newAppName = "NewAppName";
            var configProvider = new ReloadableMemoryProvider();
            configProvider.Set("Azure:SignalR:ConnectionString", $"Endpoint={originUrl};AccessKey={AccessKey};Version=1.0;");
            ServiceCollection services = new ServiceCollection();
            services.AddSignalRServiceManager(o =>
            {
                o.ApplicationName = appName;
            })
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Add(new ReloadableMemorySource(configProvider)).Build());
            using var serviceProvider = services.BuildServiceProvider();
            var contextMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ServiceManagerContext>>();
            Assert.Equal(appName, contextMonitor.CurrentValue.ApplicationName);

            configProvider.Set("Azure:SignalR:ConnectionString", $"Endpoint={newUrl};AccessKey={AccessKey};Version=1.0;");
            Assert.Equal(appName, contextMonitor.CurrentValue.ApplicationName);  // configuration via delegate is conserved after reload config.
            Assert.Equal(newUrl, contextMonitor.CurrentValue.ServiceEndpoints.Single().Endpoint);

            configProvider.Set("Azure:SignalR:ApplicationName", newAppName);
            Assert.Equal(newAppName, contextMonitor.CurrentValue.ApplicationName);
        }
    }
}