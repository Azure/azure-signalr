// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;
using AspNetTestServer = Microsoft.AspNetCore.TestHost.TestServer;
using Microsoft.AspNetCore;

namespace Microsoft.Azure.SignalR.IntegrationTests
{
    public class IntegrationTestSkeleton : VerifiableLoggedTest
    {
        private ITestOutputHelper _output;

        public IntegrationTestSkeleton(ITestOutputHelper output) : base(output)
        {
            _output = output;
        }

        [Fact]
        public async Task MockServiceConnectionHandshake()
        {
            var builder = WebHost.CreateDefaultBuilder()
                .ConfigureServices((IServiceCollection services) =>{})
                .ConfigureLogging(logging => logging.AddXunit(_output))
                .UseStartup<IntegrationTestStartup>();

            var server = new AspNetTestServer(builder);
            var host = server.Host;

            try
            {
                await host.StartAsync();
                var mockSvc = host.Services.GetRequiredService<ServiceHubDispatcher<TestHub>>() as MockServiceHubDispatcher<TestHub>;
                bool result = await mockSvc.MockService.CompletedServiceConnectionHandshake.Task;
                Assert.True(result);
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }
}
