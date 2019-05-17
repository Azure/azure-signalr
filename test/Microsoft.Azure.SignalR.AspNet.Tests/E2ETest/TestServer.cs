// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Owin.Hosting;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal class TestServer : TestServerBase
    {
        private IDisposable _webApp;

        public TestServer(ILoggerFactory loggerFactory) : base(loggerFactory)
        {
        }

        protected override void StartCore(string serverUrl)
        {
            var startOpts = new StartOptions(serverUrl);
            _webApp = WebApp.Start<TestStartup>(startOpts);
        }

        public override void Stop()
        {
            _webApp.Dispose();
        }
    }
}