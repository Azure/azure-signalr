// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Options;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class RunAzureSignalRTests
    {
        private const string ServiceUrl = "http://localhost:8086";

        [Fact]
        public void TestRunAzureSignalRWithDefaultOptions()
        {
            var hubConfig = new HubConfiguration();
            using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(hubConfig)))
            {
                var resolver = hubConfig.Resolver;
                var options = resolver.Resolve<IOptions<ServiceOptions>>();
                Assert.Null(options.Value.ConnectionString);
                Assert.Equal(typeof(ServiceHubDispatcher), resolver.Resolve<PersistentConnection>().GetType());
                Assert.Equal(typeof(ServiceEndpoint), resolver.Resolve<IServiceEndpoint>().GetType());
                Assert.Equal(typeof(ServiceConnectionManager), resolver.Resolve<IServiceConnectionManager>().GetType());
                Assert.Equal(typeof(EmptyProtectedData), resolver.Resolve<IProtectedData>().GetType());
                Assert.Equal(typeof(ServiceMessageBus), resolver.Resolve<IMessageBus>().GetType());
                Assert.Equal(typeof(AzureTransportManager), resolver.Resolve<ITransportManager>().GetType());
                Assert.Equal(typeof(ServiceProtocol), resolver.Resolve<IServiceProtocol>().GetType());
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithConnectionString()
        {
            var hubConfig = new HubConfiguration();
            var connectionString = "Endpoint=;AccessToken=;";
            using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(hubConfig, connectionString)))
            {
                var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();
                Assert.Equal(connectionString, options.Value.ConnectionString);
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithOptions()
        {
            var hubConfig = new HubConfiguration();
            var connectionString = "Endpoint=;AccessToken=;";
            using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(hubConfig, o =>
            {
                o.ConnectionString = connectionString;
                o.ConnectionCount = -1;
            })))
            {
                var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();
                Assert.Equal(connectionString, options.Value.ConnectionString);
                Assert.Equal(-1, options.Value.ConnectionCount);
            }
        }

        [Fact]
        public async Task TestNegotiateWithRunAzureSignalR()
        {
            using (WebApp.Start(ServiceUrl, a => a.RunAzureSignalR()))
            {
                var client = new HttpClient { BaseAddress = new Uri(ServiceUrl) };
                var response = await client.GetAsync("/negotiate");

                // TODO: Currently NotImplemented
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }
        }
    }
}