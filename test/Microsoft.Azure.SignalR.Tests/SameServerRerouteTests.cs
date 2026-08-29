// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests;

#nullable enable

public class SameServerRerouteTests : VerifiableLoggedTest
{
    private static int ServerCount = 0;

    private static int ClientCount = 0;

    public SameServerRerouteTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    public async Task Test()
    {
        var hub = "foo";
        var connectionString = "Endpoint=http://localhost:8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";

        using var provider = StartVerifiableLog(out var loggerFactory);

        var nameProvider = new DefaultServerNameProvider();
        var logger = loggerFactory.CreateLogger<DefaultHubProtocolResolver>();
        var hubProtocolResolver = new DefaultHubProtocolResolver(new IHubProtocol[] { new JsonHubProtocol(), new MessagePackHubProtocol() }, logger);

        var handler = new EndlessConnectionHandler<TestHub>(loggerFactory);
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
                                                   hubProtocolResolver);

        var serviceEndpoint = new ServiceEndpoint(connectionString);
        var serviceEndpointProvider = new ServiceEndpointProvider(serviceEndpoint, new ServiceOptions());
        var hubServiceEndpoint = new HubServiceEndpoint(hub, serviceEndpointProvider, serviceEndpoint);

        var messageHandler = new StrongServiceConnectionContainer(factory, 1, 1, hubServiceEndpoint, logger);
        var ackHandler = new AckHandler();

        var connection1 = factory.Create(hubServiceEndpoint, messageHandler, ackHandler, ServiceConnectionType.Default);
        var connection2 = factory.Create(hubServiceEndpoint, messageHandler, ackHandler, ServiceConnectionType.Default);

        _ = connection1.StartAsync();
        _ = connection2.StartAsync();

        await Task.Delay(1000);

        var audience = $"http://localhost/client/?hub={hub}";
        var clientPath = $"http://localhost:8080/client/?hub={hub}";
        var accessKey = ConnectionStringParser.Parse(connectionString).AccessKey;
        var accessToken = await accessKey.GenerateAccessTokenAsync(audience, Array.Empty<Claim>(), TimeSpan.FromMinutes(1), AccessTokenAlgorithm.HS256);

        var connectionOptions = new HttpConnectionOptions();
        connectionOptions.Headers.Add("Authorization", $"Bearer {accessToken}");
        var connectionFactory = new HttpConnectionFactory(Options.Create(connectionOptions), loggerFactory);
        var endpoint = new UriEndPoint(new Uri(clientPath));

        var hubConnection = new HubConnection(connectionFactory, new JsonHubProtocol(), endpoint, handler.ServiceProvider, loggerFactory);

        hubConnection.On("bar", (int x) => Assert.Equal(Interlocked.Increment(ref ClientCount), x));

        _ = hubConnection.StartAsync();

        while (true)
        {
            try
            {
                await hubConnection.SendAsync(nameof(TestHub.Foo));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            await Task.Delay(1000);
        }
    }

    private class TestHub : Hub
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
            var handler = ServiceProvider.GetRequiredService<HubConnectionHandler<TestHub>>();
            await handler.OnConnectedAsync(connection);
        }
    }

    private sealed class EndlessHubConnectionContext : HubConnectionContext
    {
        public EndlessHubConnectionContext(ConnectionContext connectionContext,
                                           HubConnectionContextOptions contextOptions,
                                           ILoggerFactory loggerFactory) : base(connectionContext, contextOptions, loggerFactory)
        {
        }
    }
}
