// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestServer : TestServerBase
    {
        private IWebHost _host;

        protected override Task StartCoreAsync(string serverUrl, ILoggerFactory loggerFactory)
        {
            _host = new WebHostBuilder()
                .UseStartup<TestStartup>()
                .UseUrls(serverUrl)
                .UseKestrel()
                .ConfigureLogging((ILoggingBuilder logging) =>
                {
                    logging.Services.AddSingleton(typeof(ILoggerFactory), loggerFactory);
                    logging.Services.AddLogging();
                })
                .Build();
            return _host.StartAsync();
        }

        public override async Task StopAsync()
        {
            await _host.StopAsync();

            // IServiceConnectionContainer is not available
            // stop server connections
            var serviceContainer = _host.Services.GetRequiredService<IServiceConnectionContainer>();
            if (serviceContainer == null)
            {
                return;
            }

            if (serviceContainer.Status != ServiceConnectionStatus.Disconnected)
            {
                await serviceContainer.StopAsync();
                return;
            }
        }
    }
}