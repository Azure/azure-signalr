// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Emulator;
using Microsoft.Azure.SignalR.Emulator.HubEmulator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Microsoft.Azure.SignalR.Common.Tests
{
    public class HubEmulatorFacts
    {
        [Fact]
        public void TestAddSignalREmulatorCanResolveDynamicHubContextStore()
        {
            var configuration = new ConfigurationBuilder().Build();
            var serviceCollection = new ServiceCollection();
            new Startup(configuration).ConfigureServices(serviceCollection);
            //serviceCollection.AddSignalREmulator().AddLogging();
            using var provider = serviceCollection.BuildServiceProvider();
            var store = provider.GetService<IDynamicHubContextStore>();
            var hubContext = store.GetOrAdd("chat");
            Assert.NotNull(hubContext.LifetimeManager);
            Assert.NotNull(hubContext.HubType);
            Assert.NotNull(hubContext.ClientManager);
            Assert.NotNull(hubContext.UserGroupManager);
        }
    }
}