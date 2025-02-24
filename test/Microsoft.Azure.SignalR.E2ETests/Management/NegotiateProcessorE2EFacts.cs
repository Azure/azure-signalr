// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.E2ETests.Management
{
    public class NegotiateProcessorE2EFacts(ITestOutputHelper outputHelper)
    {
        private readonly ITestOutputHelper _outputHelper = outputHelper;

        [ConditionalFact]
        [SkipIfConnectionStringNotPresent]
        public async Task ColdStartNegotiateTest()
        {
            var hubName = "hub";
            var services = new ServiceCollection();
            services.AddSignalRServiceManager();

            //configure two fake service endpoints and one real endpoints.
            services.Configure<ServiceManagerOptions>(o =>
            {
                o.ServiceTransportType = ServiceTransportType.Persistent;
                o.ConnectionString = TestConfiguration.Instance.ConnectionString;
                o.ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(3).ToArray();
            });

            //enable test output
            services.AddSingleton<ILoggerFactory>(new LoggerFactory([new XunitLoggerProvider(_outputHelper)])).AddSingleton<IReadOnlyCollection<ServiceDescriptor>>([.. services]);
            var manager = services.BuildServiceProvider().GetRequiredService<IServiceManager>();
            var hubContext = await manager.CreateHubContextAsync(hubName);

            var realEndpoint = new ServiceEndpoint(TestConfiguration.Instance.ConnectionString).Endpoint;
            //reduce the effect of randomness
            for (var i = 0; i < 5; i++)
            {
                var clientEndoint = await (hubContext as ServiceHubContext)!.NegotiateAsync();
                var expectedUrl = ClientEndpointUtils.GetExpectedClientEndpoint(hubName, null, realEndpoint);
                Assert.Equal(expectedUrl, clientEndoint.Url);
            }
        }
    }
}