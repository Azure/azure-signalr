// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestServer : TestServerBase
    {
        private IWebHost _host;

        public TestServer(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
        }

        protected override void StartCore(string serverUrl)
        {
            _host = new WebHostBuilder().UseStartup<TestStartup>().UseUrls(serverUrl).UseKestrel().Build();
            _host.Start();
        }

        public override void Stop()
        {
            _host.StopAsync().Wait();
            _host.Dispose();
        }
    }
}