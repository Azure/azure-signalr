// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.E2ETests.SignalR;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Azure.SignalR.Tests.Common.E2ETest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests;

internal class TestServer(ITestOutputHelper output) : TestServerBase(output)
{
    private IWebHost _host;

    private IServiceConnectionManager<TestHub> _scm;

    public IServiceProvider ServiceProvider => _host.Services;

    public override TestHubConnectionManager HubConnectionManager { get; } = new TestHubConnectionManager();

    public override async Task StopAsync()
    {
        await _host.StopAsync();
        await _scm.StopAsync();
    }

    protected override Task StartCoreAsync(string serverUrl, ITestOutputHelper output, Dictionary<string, string> configuration)
    {
        _host = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton(HubConnectionManager))
            .ConfigureLogging(logging => logging.AddXunit(output))
            .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
            .UseStartup<TestStartup>()
            .UseUrls(serverUrl)
            .UseKestrel()
            .Build();

        _scm = _host.Services.GetRequiredService<IServiceConnectionManager<TestHub>>();

        return _host.StartAsync();
    }
}