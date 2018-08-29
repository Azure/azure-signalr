// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Options;
using Microsoft.Owin.Hosting;
using Owin;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class RunAzureSignalRTests
    {
        private const string ServiceUrl = "http://localhost:8086";
        private const string ConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;";

        [Fact]
        public void TestRunAzureSignalRWithDefaultOptions()
        {
            var hubConfig = new HubConfiguration();
            using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(hubConfig, ConnectionString)))
            {
                var resolver = hubConfig.Resolver;
                var options = resolver.Resolve<IOptions<ServiceOptions>>();
                Assert.Equal(ConnectionString, options.Value.ConnectionString);
                Assert.Equal(typeof(ServiceHubDispatcher), resolver.Resolve<PersistentConnection>().GetType());
                Assert.Equal(typeof(ServiceEndpointProvider), resolver.Resolve<IServiceEndpointProvider>().GetType());
                Assert.Equal(typeof(ServiceConnectionManager), resolver.Resolve<IServiceConnectionManager>().GetType());
                Assert.Equal(typeof(EmptyProtectedData), resolver.Resolve<IProtectedData>().GetType());
                Assert.Equal(typeof(ServiceMessageBus), resolver.Resolve<IMessageBus>().GetType());
                Assert.Equal(typeof(AzureTransportManager), resolver.Resolve<ITransportManager>().GetType());
                Assert.Equal(typeof(ServiceProtocol), resolver.Resolve<IServiceProtocol>().GetType());
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithoutConnectionString()
        {
            var exception = Assert.Throws<ArgumentException>(
                () =>
                {
                    using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR()))
                    {
                    }
                });
            Assert.Equal("No connection string was specified.", exception.Message.Substring(0, 35));
        }

        [Fact]
        public void TestRunAzureSignalRWithConnectionString()
        {
            var hubConfig = new HubConfiguration();
            using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(hubConfig, ConnectionString)))
            {
                var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();
                Assert.Equal(ConnectionString, options.Value.ConnectionString);
            }
        }

        [Fact]
        public void TestRunAzureSignalRWithOptions()
        {
            var hubConfig = new HubConfiguration();
            using (WebApp.Start(ServiceUrl, app => app.RunAzureSignalR(hubConfig, o =>
            {
                o.ConnectionString = ConnectionString;
                o.ConnectionCount = -1;
            })))
            {
                var options = hubConfig.Resolver.Resolve<IOptions<ServiceOptions>>();
                Assert.Equal(ConnectionString, options.Value.ConnectionString);
                Assert.Equal(-1, options.Value.ConnectionCount);
            }
        }

        [Fact]
        public async Task TestNegotiateWithRunAzureSignalR()
        {
            using (WebApp.Start(ServiceUrl, a => a.RunAzureSignalR(ConnectionString)))
            {
                var client = new HttpClient { BaseAddress = new Uri(ServiceUrl) };
                var response = await client.GetAsync("/negotiate");

                // TODO: Currently NotImplemented
                Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            }
        }
    }
}