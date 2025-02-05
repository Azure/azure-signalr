// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.E2ETests.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.E2ETests.ServerSDK;

[SkipIfConnectionStringNotPresent]
public class SameServerRerouteTests(ITestOutputHelper output) : VerifiableLoggedTest(output)
{
    private static int ServerCount;

    private static int ClientCount;

    private readonly TestHubConnectionFactory _hubConnectionFactory = new();

    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task TestIngressReload()
    {
        using var server = new TestServer(_output);

        var host = await server.StartAsync();

        const string method = nameof(TestHub.Echo);
        const int ConnectionCount = 10;
        const int MessagePerConnection = 1000;

        // wait server connection connected.
        await Task.Delay(1000);
        var connections = _hubConnectionFactory.NewConnectionGroup(host, ConnectionCount);
        connections.Listen(method);
        await connections.StartAsync();

        _ = TriggerIngressReloadAfter(IngressHeaderProvider.PodA, 5000);

        for (var i = 0; i < MessagePerConnection; i++)
        {
            await connections.SendAsync(method, "foo");
            await Task.Delay(10);
        }

        Assert.Equal(ConnectionCount * MessagePerConnection, connections.MessageCount);

        await server.StopAsync();
    }

    [ConditionalFact]
    public async Task Test()
    {
        const string hub = "foo";

        using var provider = StartVerifiableLog(out var loggerFactory);

        var nameProvider = new DefaultServerNameProvider();
        var logger = loggerFactory.CreateLogger<DefaultHubProtocolResolver>();
        var hubProtocolResolver = new DefaultHubProtocolResolver([new JsonHubProtocol()], logger);

        var handler = new EndlessConnectionHandler<LocalHub>(loggerFactory);
        ConnectionDelegate connectionDelegate = handler.OnConnectedAsync;

        var factory = new ServiceConnectionFactory(new ServiceProtocol(),
                                                   new ClientConnectionManager(),
                                                   new ConnectionFactory(nameProvider, loggerFactory),
                                                   loggerFactory,
                                                   connectionDelegate,
                                                   new ClientConnectionFactory(loggerFactory),
                                                   nameProvider,
                                                   new DefaultServiceEventHandler(loggerFactory),
                                                   new DummyClientInvocationManager(),
                                                   new DefaultHeaderProvider(),
                                                   hubProtocolResolver);

        var serviceEndpoint = new ServiceEndpoint(TestConfiguration.Instance.ConnectionString);
        var serviceEndpointProvider = new ServiceEndpointProvider(serviceEndpoint, new ServiceOptions());
        var hubServiceEndpoint = new HubServiceEndpoint(hub, serviceEndpointProvider, serviceEndpoint);

        var messageHandler = new StrongServiceConnectionContainer(factory, 1, 1, hubServiceEndpoint, logger);
        var ackHandler = new AckHandler();

        var connection1 = factory.Create(hubServiceEndpoint, messageHandler, ackHandler, ServiceConnectionType.Default);
        var connection2 = factory.Create(hubServiceEndpoint, messageHandler, ackHandler, ServiceConnectionType.Default);

        var serverConnectionTask1 = connection1.StartAsync();
        var serverConnectionTask2 = connection2.StartAsync();

        await Task.Delay(1000);

        var connectionString = TestConfiguration.Instance.ConnectionString;
        var audience = $"http://localhost/client/?hub={hub}";
        var clientPath = $"http://localhost:8080/client/?hub={hub}";
        var accessKey = new AccessKey(ConnectionStringParser.Parse(connectionString).AccessKey);
        var accessToken = await accessKey.GenerateAccessTokenAsync(audience, [], TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);

        var connectionOptions = new HttpConnectionOptions();
        connectionOptions.Headers.Add("Authorization", $"Bearer {accessToken}");
        var connectionFactory = new HttpConnectionFactory(Options.Create(connectionOptions), loggerFactory);
        var endpoint = new UriEndPoint(new Uri(clientPath));

        var hubConnection = new HubConnection(connectionFactory, new JsonHubProtocol(), endpoint, handler.ServiceProvider, loggerFactory);
        hubConnection.On("bar", (int x) =>
        {
            Assert.Equal(Interlocked.Increment(ref ClientCount), x);
        });
        _ = hubConnection.StartAsync();

        while (true)
        {
            try
            {
                await hubConnection.SendAsync(nameof(LocalHub.Foo));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            await Task.Delay(1000);
        }
    }

    private static async Task TriggerIngressReloadAfter(string podName, int millisecondsDelay)
    {
        await Task.Delay(millisecondsDelay);

        var url = $"http://localhost:5003/notify/ingress-controller/reload?podName={podName}";

        var httpClient = new HttpClient();
        await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(podName)
        });
    }

    public class IngressRequest
    {
        [JsonProperty("podName")]
        public string PodName { get; set; } = string.Empty;
    }

    private sealed class LocalHub : Hub
    {
        public Task Foo()
        {
            var index = Interlocked.Increment(ref ServerCount);
            return Clients.Caller.SendAsync("bar", index);
        }

        public override Task OnConnectedAsync()
        {
            return Task.CompletedTask;
        }
    }

    private sealed class EndlessConnectionHandler<THub> : ConnectionHandler where THub : Hub
    {
        public ServiceProvider ServiceProvider { get; }

        public EndlessConnectionHandler(ILoggerFactory loggerFactory)
        {
            var collection = new ServiceCollection();
            collection.AddLogging();
            collection.AddSingleton(loggerFactory);
            collection.AddSingleton<IUserIdProvider, DefaultUserIdProvider>();
            collection.AddSingleton<HubConnectionHandler<THub>>();
            collection.AddSignalR().AddJsonProtocol();

            ServiceProvider = collection.BuildServiceProvider();
        }

        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            var handler = ServiceProvider.GetRequiredService<HubConnectionHandler<LocalHub>>();
            await handler.OnConnectedAsync(connection);
        }
    }

    private sealed class EndlessHubConnectionContext(ConnectionContext connectionContext,
                                                     HubConnectionContextOptions contextOptions,
                                                     ILoggerFactory loggerFactory) : HubConnectionContext(connectionContext, contextOptions, loggerFactory)
    {
    }
}
