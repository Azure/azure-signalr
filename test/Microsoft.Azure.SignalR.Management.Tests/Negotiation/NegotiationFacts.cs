// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    /// <summary>
    /// Contains negotiation tests that need cooperation of multiple classes.
    /// </summary>
    public class NegotiationFacts
    {
        private readonly ILoggerFactory _loggerFactory;
        public NegotiationFacts(ITestOutputHelper testOutput)
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddXunit(testOutput);
        }
        [Theory]
        [InlineData(ServiceTransportType.Persistent)]
        [InlineData(ServiceTransportType.Transient)]
        public async Task HubContextNotCheckEndpointHealthWithSingleEndpoint(ServiceTransportType serviceTransportType)
        {
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ServiceTransportType = serviceTransportType;
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).First();
                })
                .WithLoggerFactory(_loggerFactory)
                .BuildServiceManager();
            var hubContext = await serviceManager.CreateHubContextAsync("hubName", default);
            var negotiateResponse = await hubContext.NegotiateAsync();
            Assert.NotNull(negotiateResponse);
        }

        [Theory]
        [InlineData(ServiceTransportType.Persistent)]
        [InlineData(ServiceTransportType.Transient)]
        public async Task StrongTypedHubContextNotCheckEndpointHealthWithSingleEndpoint(ServiceTransportType serviceTransportType)
        {
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ServiceTransportType = serviceTransportType;
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).First();
                })
                .WithLoggerFactory(_loggerFactory)
                .BuildServiceManager();
            var hubContext = await serviceManager.CreateHubContextAsync<IChat>("hubName", default);
            var negotiateResponse = await hubContext.NegotiateAsync();
            Assert.NotNull(negotiateResponse);
        }

        [Theory]
        [InlineData(ServiceTransportType.Persistent)]
        //[InlineData(ServiceTransportType.Transient)] Not implemented yet
        public async Task HubContextCheckEndpointHealthWithMultiEndpoints(ServiceTransportType serviceTransportType)
        {
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ServiceTransportType = serviceTransportType;
                    o.ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(2).ToArray();
                })
                .WithLoggerFactory(_loggerFactory)
                .BuildServiceManager();
            var hubContext = await serviceManager.CreateHubContextAsync("hubName", default);
            await Assert.ThrowsAsync<AzureSignalRNotConnectedException>(() => hubContext.NegotiateAsync().AsTask());
        }

        [Theory]
        [InlineData(ServiceTransportType.Persistent)]
        //[InlineData(ServiceTransportType.Transient)] Not implemented yet
        public async Task StrongTypedHubContextCheckEndpointHealthWithMultiEndpoints(ServiceTransportType serviceTransportType)
        {
            var serviceManager = new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ServiceTransportType = serviceTransportType;
                    o.ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(2).ToArray();
                })
                .WithLoggerFactory(_loggerFactory)
                .BuildServiceManager();
            var hubContext = await serviceManager.CreateHubContextAsync<IChat>("hubName", default);
            await Assert.ThrowsAsync<AzureSignalRNotConnectedException>(() => hubContext.NegotiateAsync().AsTask());
        }

        public interface IChat
        {
            Task NewMessage(string message);
        }
    }
}
