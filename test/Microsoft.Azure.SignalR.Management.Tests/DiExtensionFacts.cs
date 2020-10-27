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

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class DiExtensionFacts
    {
        private const string AccessKey = "nOu3jXsHnsO5urMumc87M9skQbUWuQ+PE5IvSUEic8w=";

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

            Thread.Sleep(1000);
            Assert.Equal(newUrl, optionsMonitor.CurrentValue.ServiceEndpoints.Single().Endpoint);
        }
    }
}