// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestServer : TestServerBase
    {
        private IWebHost _host;

        protected override Task StartCoreAsync(string serverUrl, ILoggerFactory loggerFactory)
        {
            TestHub.ClearConnectedConnectionAndUser();

            _host = new WebHostBuilder()
                .UseStartup<TestStartup>()
                .UseUrls(serverUrl)
                .UseKestrel()
                .ConfigureServices(c => c.AddSingleton<ILoggerFactory>(loggerFactory))
                .Build();
            return _host.StartAsync();
        }

        public override async Task StopAsync()
        {

            await _host.StopAsync();

            //// dispose client connections
            //var clientConnections = _host.Services.GetRequiredService<IClientConnectionManager>().ClientConnections;
            //clientConnections.Clear();

            //// stop server connections
            //await _host.Services.GetRequiredService<IServiceConnectionManager<TestHub>>().StopAsync();

            _host.Dispose();
        }
    }
}