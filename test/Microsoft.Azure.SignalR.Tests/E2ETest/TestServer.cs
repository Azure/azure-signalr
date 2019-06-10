// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
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
            TestHub.ClearConnectedConnectionAndUser();

            _host = new WebHostBuilder()
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