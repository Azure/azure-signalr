// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class DiExtensionFacts
    {
        private const string Url = "https://abc";
        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";
        private static readonly string TestConnectionString = $"Endpoint={Url};AccessKey={AccessKey};Version=1.0;";

        private readonly ITestOutputHelper _outputHelper;

        public DiExtensionFacts(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public void ConfigureByFile_ChangeDetecedFact()
        {
            string configPath = "temp.json";
            var originUrl = "http://abc";
            var newUrl = "http://cde";
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
            _outputHelper.WriteLine(JsonConvert.SerializeObject(configObj));
            ServiceCollection services = new ServiceCollection();
            services.AddSignalRServiceManager();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddJsonFile(configPath, false, true).Build());
            using var provider = services.BuildServiceProvider();
            var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ServiceOptions>>();
            Assert.Equal(originUrl, optionsMonitor.CurrentValue.Endpoints.Single().Endpoint);

            //update json config file
            configObj.Azure.SignalR.ConnectionString = $"Endpoint={newUrl};AccessKey={AccessKey};Version=1.0;";
            File.WriteAllText(configPath, JsonConvert.SerializeObject(configObj));

            Thread.Sleep(3000);
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
            _outputHelper.WriteLine(productInfo);
        }

        [Fact]
        public void ConfigureByDelegateFact_Method1()
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
        public void ConfigureByDelegateFact_Method2()
        {
            ServiceCollection services = new ServiceCollection();
            services.Configure<ServiceManagerOptions>(o => o.ConnectionString = TestConnectionString);
            services.Configure<ServiceManagerOptions>(o => o.ServiceTransportType = ServiceTransportType.Persistent);
            services.AddSignalRServiceManagerCore();
            using var serviceProvider = services.BuildServiceProvider();
            var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ServiceManagerContext>>();
            Assert.Equal(Url, optionsMonitor.CurrentValue.ServiceEndpoints.Single().Endpoint);
            Assert.Equal(ServiceTransportType.Persistent, optionsMonitor.CurrentValue.ServiceTransportType);
        }
    }
}