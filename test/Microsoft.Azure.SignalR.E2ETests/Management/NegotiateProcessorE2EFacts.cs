// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
        [SkipIfConnectionStringNotPresent]
        public async Task ColdStartNegotiateTest()
        {
            var hubName = "hub";
            ServiceCollection services = new ServiceCollection();
            services.AddSignalRServiceContext<ContextOptionsSetup>();

            //configure two fake service endpoints and one real endpoints.
            services.Configure<ContextOptions>(o =>
            {
                o.ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(3).Append(new ServiceEndpoint(TestConfiguration.Instance.ConnectionString)).ToArray();
            });

            //enable test output
            services.AddSingleton<ILoggerFactory>(new LoggerFactory(new List<ILoggerProvider> { new XunitLoggerProvider(_outputHelper) }));
            var serviceProvider = services.BuildServiceProvider();
            var negotiateProcessor = serviceProvider.GetRequiredService<NegotiateProcessor>();

            var realEndpoint = new ServiceEndpoint(TestConfiguration.Instance.ConnectionString).Endpoint;
            //reduce the effect of randomness
            for (int i = 0; i < 5; i++)
            {
                var clientEndoint = await negotiateProcessor.NegotiateAsync(hubName);
                var expectedUrl = ClientEndpointUtils.GetExpectedClientEndpoint(hubName, null, realEndpoint);
                Assert.Equal(expectedUrl, clientEndoint.Url);
            }
        }
    }
}