﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestServer : TestServerBase
    {
        private IWebHost _host;

        public override TestHubConnectionManager HubConnectionManager { get; }

        public TestServer(ITestOutputHelper output): base(output)
        {
            HubConnectionManager = new TestHubConnectionManager();
        }

        protected override Task StartCoreAsync(string serverUrl, ITestOutputHelper output, Dictionary<string, string> configuration)
        {
            _host = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<TestHubConnectionManager>(HubConnectionManager);
                })
                .ConfigureLogging(logging => logging.AddXunit(output))
                .ConfigureAppConfiguration(builder =>  builder.AddInMemoryCollection(configuration))
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