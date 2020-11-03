using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceManagerBuilderFacts
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
            var serviceProvider = builder.GetServiceProvider();
            var productInfo = serviceProvider.GetRequiredService<IOptions<ServiceManagerContext>>().Value.ProductInfo;
            Assert.Matches("^Microsoft.Azure.SignalR.Management.Tests/", productInfo);
        }

        [Fact(Skip = "The test fails for unknown reason in GitHub Actions.")]
        public async Task ConfigHotReloadTest()
        {
            string configPath = "temp.json";
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
            using var provider = builder.GetServiceProvider();
            var optionsMonitor = provider.GetRequiredService<IOptionsMonitor<ServiceOptions>>();
            Assert.Equal(originUrl, optionsMonitor.CurrentValue.Endpoints.Single().Endpoint);

            //update json config file
            configObj.Azure.SignalR.ConnectionString = $"Endpoint={newUrl};AccessKey={AccessKey};Version=1.0;";
            File.WriteAllText(configPath, JsonConvert.SerializeObject(configObj));

            await Task.Delay(5000);
            Assert.Equal(newUrl, optionsMonitor.CurrentValue.Endpoints.Single().Endpoint);
            _outputHelper.WriteLine("This test may fail in github-actions/Gated -Windows. It should be OK.");
        }
    }
}