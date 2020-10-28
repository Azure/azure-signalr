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
        private static readonly string _testConnectionString = $"Endpoint={Url};AccessKey={AccessKey};Version=1.0;";
        private static readonly string[] Urls = Enumerable.Range(0, 2).Select(id => $"https://endpoint-{id}").ToArray();

        private readonly ITestOutputHelper _outputHelper;

        public DiExtensionFacts(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public void ServiceEndpointsConfigurationChangeDetecedFact()

        {
            string configPath = "temp.json";
            var originUrl = "http://abc";
            var newUrl = "http://cde";
            var serviceManagerOptions = new ServiceManagerOptions()
            {
                ConnectionString = $"Endpoint={originUrl};AccessKey={AccessKey};Version=1.0;"
            };
            File.WriteAllText(configPath, JsonConvert.SerializeObject(serviceManagerOptions));
            ServiceCollection services = new ServiceCollection();
            services.Configure<ServiceManagerOptions>(new ConfigurationBuilder().AddJsonFile(configPath, false, true).Build());
            services.AddSignalRServiceManager();
            using var provider = services.BuildServiceProvider();
            var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ServiceManagerContext>>();
            Assert.Equal(originUrl, optionsMonitor.CurrentValue.ServiceEndpoints.Single().Endpoint);

            //update json config file
            serviceManagerOptions.ConnectionString = $"Endpoint={newUrl};AccessKey={AccessKey};Version=1.0;";
            File.WriteAllText(configPath, JsonConvert.SerializeObject(serviceManagerOptions));

            Thread.Sleep(5000);
            Assert.Equal(newUrl, optionsMonitor.CurrentValue.ServiceEndpoints.Single().Endpoint);
        }

        [Fact]
        public void ProductInfoDefaultValueNotNullFact()
        {
            ServiceCollection services = new ServiceCollection();
            services.Configure<ServiceManagerOptions>(o =>
            {
                o.ConnectionString = _testConnectionString;
                o.ServiceTransportType = ServiceTransportType.Persistent;
            });
            services.AddSignalRServiceManager();
            using var serviceProvider = services.BuildServiceProvider();
            var productInfo = serviceProvider.GetRequiredService<IOptions<ServiceManagerContext>>().Value.ProductInfo;
            Assert.Matches("^Microsoft.Azure.SignalR.Management/", productInfo);
            _outputHelper.WriteLine(productInfo);
        }

        [Fact]
        public void ConfigureViaDelegateFact()
        {
            ServiceCollection services = new ServiceCollection();
            services.Configure<ServiceManagerOptions>(o =>
            {
                o.ConnectionString = _testConnectionString;
                o.ServiceTransportType = ServiceTransportType.Persistent;
            });
            services.AddSignalRServiceManager();
            using var serviceProvider = services.BuildServiceProvider();
            var optionsMonitor = serviceProvider.GetRequiredService<IOptionsMonitor<ServiceOptions>>();
            Assert.Equal(Url, optionsMonitor.CurrentValue.Endpoints.Single().Endpoint);
        }
    }
}