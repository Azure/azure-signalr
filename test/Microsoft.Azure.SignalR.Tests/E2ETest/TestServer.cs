// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestServer : TestServerBase
    {
        private IWebHost _host;

        public TestServer(ITestOutputHelper output): base(output)
        {
        }

        protected override Task StartCoreAsync(string serverUrl, ITestOutputHelper output)
        {
            var testHubConnectionManager = new TestHubConnectionManager();

            _host = new WebHostBuilder()
                .ConfigureServices(services => services.AddSingleton<TestHubConnectionManager>(testHubConnectionManager))
                .ConfigureLogging(logging => logging.AddXunit(output))
                .UseStartup<TestStartup>()
                .UseUrls(serverUrl)
                .UseKestrel()
                .Build();
            return _host.StartAsync();
        }

        public override async Task StopAsync()
        {
            await _host.StopAsync();
        }
    }
}