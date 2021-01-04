// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class MultiEndpointConnectionContainerFactoryFacts
    {
        private const string Hub = nameof(Hub);

        [Fact]
        public void CreateDirectContainer_WithReferenceNotEqualEndpoints()
        {
            //prepare endpoints
            var totalCount = 3;
            var selectedCount = 2;
            var endpoints = FakeEndpointUtils.GetFakeEndpoint(totalCount).ToArray();
            var targetEndpoints = endpoints.Take(selectedCount).Select(endpoint => new ServiceEndpoint(endpoint));

            //create services
            var serviceProvider = new ServiceCollection().AddSignalRServiceContext<ContextOptionsSetup>()
                .Configure<ContextOptions>(o => o.ServiceEndpoints = endpoints)
                .BuildServiceProvider();
            var factory = serviceProvider.GetRequiredService<MultiEndpointConnectionContainerFactory>();
            var endpointManager = serviceProvider.GetRequiredService<IServiceEndpointManager>();
            var hubEndpoints = endpointManager.GetEndpoints(Hub);

            var container = factory.Create(Hub, targetEndpoints);
            var innerEndpoints = container.GetRoutedEndpoints(null).Select(e => e as HubServiceEndpoint).Where(e => e != null).ToArray();
            Assert.True(innerEndpoints.SequenceEqual(hubEndpoints.Take(selectedCount)));
        }

        [Fact]
        public void CreateDirectContainer_WithInvalidEndpoints()
        {
            //prepare endpoints
            var fakeEndpoints = FakeEndpointUtils.GetFakeEndpoint(3);
            var endpoints = fakeEndpoints.Take(2).ToArray();
            var targetEndpoints = fakeEndpoints.Skip(2).ToArray();

            //create services
            var serviceProvider = new ServiceCollection().AddSignalRServiceContext<ContextOptionsSetup>()
                .Configure<ContextOptions>(o => o.ServiceEndpoints = endpoints)
                .BuildServiceProvider();
            var factory = serviceProvider.GetRequiredService<MultiEndpointConnectionContainerFactory>();
            var endpointManager = serviceProvider.GetRequiredService<IServiceEndpointManager>();
            var hubEndpoints = endpointManager.GetEndpoints(Hub);

            var exc = Assert.Throws<AzureSignalRInvalidEndpointException>(() => factory.Create(Hub, targetEndpoints));
            var invalidEndpoint = (exc.Data[typeof(ServiceEndpoint).FullName] as ServiceEndpoint[]).Single();
            Assert.Equal(targetEndpoints.Single(), invalidEndpoint);
        }
    }
}