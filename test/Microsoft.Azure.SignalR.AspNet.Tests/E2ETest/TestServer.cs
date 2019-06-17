// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal sealed class UserIdProvider : IUserIdProvider
    {
        public string GetUserId(IRequest request)
        {
            return request.QueryString["user"];
        }
    }

    internal class TestServer : TestServerBase
    {
        private IDisposable _webApp;
        private ILoggerFactory _loggerFactory;

        public TestServer(ITestOutputHelper output) : base(output)
        {
        }

        protected override Task StartCoreAsync(string serverUrl, ITestOutputHelper output)
        {
            var testHubConnectionManager = new TestHubConnectionManager();
            var userIdProvider = new UserIdProvider();
            _loggerFactory = new LoggerFactory().AddXunit(output);

            _webApp = WebApp.Start(new StartOptions(serverUrl), app =>
            {
                var hubConfiguration = Utility.GetActualHubConfig(_loggerFactory);
                hubConfiguration.Resolver.Register(typeof(TestHub), () => new TestHub(testHubConnectionManager));
                hubConfiguration.Resolver.Register(typeof(IUserIdProvider), () => userIdProvider);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                app.MapAzureSignalR("/signalr", GetType().FullName, hubConfiguration, options => options.ConnectionString = TestConfiguration.Instance.ConnectionString);
                GlobalHost.TraceManager.Switch.Level = SourceLevels.Information;
            });
            return Task.CompletedTask;
        }

        public override Task StopAsync()
        {
            _webApp?.Dispose();
            _loggerFactory?.Dispose();
            return Task.CompletedTask;
        }
    }
}