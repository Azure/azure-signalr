// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR.TestsCommon;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class ServiceHubContextE2EFacts
    {
        private const string HubName = "signalrBench";
        private const string MethodName = "SendMessage";
        private const string Message = "Hello client, have a nice day!";
        private const int ClientConnectionCount = 2;
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

        [ConditionalTheory]
        [SkipIfConnectionStringNotPresent]
        [InlineData(ServiceTransportType.Transient)]
        internal async Task BroadcastTest(ServiceTransportType serviceTransportType)
        {
            var serviceManager = Utility.GenerateServiceManager(TestConfiguration.Instance.ConnectionString, serviceTransportType);
            var serviceHubContext = await serviceManager.CreateHubContextAsync(HubName);

            var clientEndpoint = serviceManager.GetClientEndpoint(HubName);
            var clientAccessToken = serviceManager.GenerateClientAccessToken(HubName);

            var connections = await CreateAndStartClientConnections(clientEndpoint, clientAccessToken);
            var receivedMessageCount = 0;
            ListenOnMessage(connections, () => receivedMessageCount++);

            Task task = null;
            try
            {
                await (task = serviceHubContext.Clients.All.SendAsync(MethodName, Message));
            }
            finally
            {
                Assert.Null(task.Exception);
            }

            await Task.Delay(_timeout);

            Assert.Equal(ClientConnectionCount, receivedMessageCount);
        }

        private static async Task<IEnumerable<HubConnection>> CreateAndStartClientConnections(string clientEndpoint, string clientAccessToken)
        {
            var connections = new List<HubConnection>(ClientConnectionCount);
            for (var i = 0; i < ClientConnectionCount; i++)
            {
                connections.Add(Utility.CreateHubConnection(clientEndpoint, clientAccessToken));
            }

            await Task.WhenAll(from connection in connections
                               select connection.StartAsync());

            return connections;
        }

        private void ListenOnMessage(IEnumerable<HubConnection> connections, Action increaseReceivedMassageCount)
        {
            foreach(var connection in connections)
            {
                connection.On(MethodName, (string message) =>
                {
                    increaseReceivedMassageCount.Invoke();
                    Assert.Equal(Message, message);
                });
            }
        }
    }
}
