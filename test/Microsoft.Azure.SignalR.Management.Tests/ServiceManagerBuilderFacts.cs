// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
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
    public partial class ServiceManagerBuilderFacts
    {
        private const string Endpoint = "https://abc";
        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";
        private static readonly string _testConnectionString = $"Endpoint={Endpoint};AccessKey={AccessKey};Version=1.0;";
        private readonly ITestOutputHelper _outputHelper;

        public ServiceManagerBuilderFacts(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public void WithCallingAssemblyTest()
        {
            var builder = new ServiceManagerBuilder().WithCallingAssembly().WithOptions(o => o.ConnectionString = _testConnectionString);
            builder.Build();
            var serviceProvider = builder.ServiceProvider;
            var productInfo = serviceProvider.GetRequiredService<IOptions<ServiceManagerContext>>().Value.ProductInfo;
            Assert.Matches("^Microsoft.Azure.SignalR.Management.Tests/", productInfo);
        }

        [Fact]
        public async Task FileConfigHotReloadTest()
        {
            // to avoid possible file name conflict with another FileConfigHotReloadTest
            string configPath = nameof(ServiceManagerBuilderFacts);
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
            var builder = new ServiceManagerBuilder().WithConfiguration(new ConfigurationBuilder().AddJsonFile(configPath, false, true).Build());
            builder.Build();
            using var provider = builder.ServiceProvider;
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
            var builder = new ServiceManagerBuilder().WithConfiguration(new ConfigurationBuilder().Add(new ReloadableMemorySource(configProvider)).Build());
            builder.Build();
            using var provider = builder.ServiceProvider;
            var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ServiceOptions>>();
            Assert.Equal(originUrl, optionsMonitor.CurrentValue.Endpoints.Single().Endpoint);

            //update
            configProvider.Set("Azure:SignalR:ConnectionString", $"Endpoint={newUrl};AccessKey={AccessKey};Version=1.0;");
            Assert.Equal(newUrl, optionsMonitor.CurrentValue.Endpoints.Single().Endpoint);
        }
    }
}