// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests;
public class MessageOrderTest
{
    private readonly ITestOutputHelper _output;

    public MessageOrderTest(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// First, set up a client to send a fixed number of messages. Now the first service connection is used to send messages.
    /// Then in the middle of sending, disconnect one of the service connections and the second service connections should be used to send message.
    /// </summary>
    [Fact]
    public async Task TestMessageOrderWithSequentialSending()
    {
        async Task testAction(ServiceHubContext hubContext, TestServiceConnectionFactory testConnectionFactory)
        {
            var sendTask = SendingTask(hubContext);

            await Task.Delay(7 * 100);
            foreach (var connections in testConnectionFactory.CreatedConnections.Values)
            {
                (connections.First() as TestServiceConnection).SetStatus(ServiceConnectionStatus.Disconnected);
            }

            await sendTask;

            foreach (var connections in testConnectionFactory.CreatedConnections.Values)
            {
                var expectedIndex = 0;

                foreach (var message in (connections[0] as TestServiceConnection).ReceivedMessages)
                {
                    Assert.Equal(expectedIndex.ToString(), (message as BroadcastDataMessage).ExcludedList.Single());
                    expectedIndex++;
                }

                Assert.True(21 > expectedIndex);

                foreach (var message in (connections[1] as TestServiceConnection).ReceivedMessages)
                {
                    Assert.Equal(expectedIndex.ToString(), (message as BroadcastDataMessage).ExcludedList.Single());
                    expectedIndex++;
                }
                Assert.Equal(21, expectedIndex);
            }
        }
        await MockConnectionTestAsync(testAction);
    }


    private static async Task SendingTask(ServiceHubContext hubContext)
    {
        for (var i = 0; i < 21; i++)
        {
            await hubContext.Clients.AllExcept(new string[] { i.ToString() }).SendAsync("Send");
            await Task.Delay(300);
        }
    }

    private async Task MockConnectionTestAsync(Func<ServiceHubContext, TestServiceConnectionFactory, Task> testAction)
    {
        var connectionFactory = new TestServiceConnectionFactory();
        var serviceManager = new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ServiceTransportType = ServiceTransportType.Persistent;
                o.ServiceEndpoints = FakeEndpointUtils.GetFakeEndpoint(2).ToArray();
                o.ConnectionCount = 3;
            })
            .WithLoggerFactory(new LoggerFactory().AddXunit(_output))
            .ConfigureServices(services => services.AddSingleton<IServiceConnectionFactory>(connectionFactory))
            .BuildServiceManager();
        var hubContext = await serviceManager.CreateHubContextAsync("hub1", default);

        await testAction.Invoke(hubContext, connectionFactory);
    }
}
