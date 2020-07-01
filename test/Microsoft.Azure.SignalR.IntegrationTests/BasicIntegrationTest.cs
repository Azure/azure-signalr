// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Azure.SignalR.IntegrationTests.Infrastructure;

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
            Console.WriteLine();
            var host = new WebHostBuilder()
               .ConfigureServices(services =>{})
               .ConfigureLogging(logging => logging.AddXunit(_output))
               .UseStartup<IntegrationTestStartup>()
               .UseUrls("http://localhost:8901")
               .UseKestrel()
               .Build();

            await host.StartAsync();

            var mockSvc = host.Services.GetRequiredService<ServiceHubDispatcher<TestHub>>() as MockServiceHubDispatcher<TestHub>;
            Console.WriteLine(mockSvc);
            bool result = await mockSvc.MockService.CompletedServiceConnectionHandshake.Task;
            Assert.True(result);
            await host.StopAsync();
        }
    }
}
