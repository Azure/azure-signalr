using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Internal;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceLifetimeManagerIT
    {
        private static readonly List<string> TestUsers = new List<string> { "TestUser" };

        private static readonly List<string> TestGroups = new List<string> { "TestGroup" };

        private static readonly string TestMethod = "TestMethod";

        private static readonly object[] TestArgs = new[] { "TestArgs" };

        private static readonly List<string> TestConnectionIds = new List<string> { "connectionId1" };

        [Fact]
        public async void SendAllAsync()
        {
            var proxy = new ServiceConnectionProxy();

            var serviceConnectionManager = new ServiceConnectionManager<TestHub>();
            serviceConnectionManager.AddServiceConnection(proxy.ServiceConnection);

            var serviceLifetimeManager = new ServiceLifetimeManager<TestHub>(serviceConnectionManager,
                proxy.ClientConnectionManager,
                new DefaultHubProtocolResolver(new IHubProtocol[] { new JsonHubProtocol(), new MessagePackHubProtocol() }, NullLogger<DefaultHubProtocolResolver>.Instance),
                NullLogger<ServiceLifetimeManager<TestHub>>.Instance
                );

            await proxy.StartAsync().OrTimeout();

            Task backendTask = proxy.ProcessIncomingAsync();

            Task task = proxy.WaitForSpecificMessage(typeof(BroadcastDataMessage));

            await serviceLifetimeManager.SendAllAsync(TestMethod, TestArgs);

            await task.OrTimeout();
        }
    }
}
