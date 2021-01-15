// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceHubContextFacts
    {
        private const string Hub = nameof(Hub);

        [Fact]
        public async Task CreateDirectContainer_WithReferenceNotEqualEndpoints()
        {
            //prepare endpoints
            var totalCount = 3;
            var selectedCount = 2;
            var endpoints = FakeEndpointUtils.GetFakeEndpoint(totalCount).ToArray();
            var targetEndpoints = endpoints.Take(selectedCount).Select(endpoint => new ServiceEndpoint(endpoint));

            //create services
            var services = new ServiceCollection().AddSignalRServiceManager()
                .Configure<ServiceManagerOptions>(o =>
                {
                    o.ServiceEndpoints = endpoints;
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                });
            services.AddSingleton<IReadOnlyCollection<ServiceDescriptor>>(services.ToList());
            var serviceProvider = services.BuildServiceProvider();

            var hubContext = (await serviceProvider.GetRequiredService<IServiceManager>().CreateHubContextAsync(Hub)
                as IInternalServiceHubContext)
                .WithEndpoints(targetEndpoints);
            var container = (hubContext as ServiceHubContext)
                .ServiceProvider.GetRequiredService<IServiceConnectionContainer>()
                as DirectMultiEndpointServiceConnectionContainer;
            var innerEndpoints = container.GetRoutedEndpoints(null).Select(e => e as HubServiceEndpoint).Where(e => e != null).ToArray();
            var hubEndpoints = serviceProvider.GetRequiredService<IServiceEndpointManager>().GetEndpoints(Hub);
            Assert.True(innerEndpoints.SequenceEqual(hubEndpoints.Take(selectedCount)));
        }
    }
}