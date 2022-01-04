// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.E2ETests
{
    public class NegotiateProcessorE2EFacts
    {
        private readonly ITestOutputHelper _outputHelper;

        public NegotiateProcessorE2EFacts(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [ConditionalFact]
        [SkipIfEndpointNotPresent]
        public async Task ColdStartNegotiateTest()
        {
            var hubName = "hub";
            var services = new ServiceCollection();
            services.AddSignalRServiceManager();

            var expected = new ServiceEndpoint(new Uri(TestConfiguration.Instance.TestEndpoint),
                                               new DefaultAzureCredential());

            // configure three fake service endpoints and one real endpoints.
            services.Configure<ServiceManagerOptions>(o =>
            {
                o.ServiceTransportType = ServiceTransportType.Persistent;
                o.ServiceEndpoints = FakeEndpointUtils.GetFakeAzureAdEndpoint(3).Concat(new ServiceEndpoint[] { expected }).ToArray();
            });

            // enable test output
            var serviceCollection = services.AddSingleton<ILoggerFactory>(new LoggerFactory(new List<ILoggerProvider> { new XunitLoggerProvider(_outputHelper) })).AddSingleton<IReadOnlyCollection<ServiceDescriptor>>(services.ToList());
            var manager = services.BuildServiceProvider().GetRequiredService<IServiceManager>();
            var hubContext = await manager.CreateHubContextAsync(hubName);

            // reduce the effect of randomness
            for (var i = 0; i < 5; i++)
            {
                var clientEndoint = await (hubContext as ServiceHubContext).NegotiateAsync();
                var expectedUrl = ClientEndpointUtils.GetExpectedClientEndpoint(hubName, null, expected.Endpoint);
                Assert.Equal(expectedUrl, clientEndoint.Url);
            }
        }
    }
}
