// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class MicrosoftEntraProxyFacts
{
    [Fact]
    public async Task GetAccessKeyRequestGoesThroughConfiguredProxy()
    {
        // A fake HTTP proxy: the request must arrive here in absolute-form, addressed to the (unresolvable) service host.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var proxyPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            var proxyTask = ServeOnceAsync(listener);

            var endpoint = new ServiceEndpoint(new Uri("http://signalr.invalid"), new StaticTokenCredential());
            using var services = CreateServices(endpoint, new WebProxy($"http://127.0.0.1:{proxyPort}"));
            var manager = services.GetRequiredService<IServiceEndpointManager>();
            var hubEndpoint = Assert.Single(manager.GetEndpoints("hub"));
            var provider = manager.GetEndpointProvider(hubEndpoint);
            var secondProvider = manager.GetEndpointProvider(Assert.Single(manager.GetEndpoints("secondHub")));
            Assert.Same(hubEndpoint.AccessKey, provider.AccessKey);
            Assert.Same(provider.AccessKey, secondProvider.AccessKey);

            var tokenTask = Task.WhenAll(provider.GenerateClientAccessTokenAsync("hub"), secondProvider.GenerateClientAccessTokenAsync("secondHub"));

            var requestHead = await proxyTask.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.StartsWith("GET http://signalr.invalid/api/v1/auth/accessKey ", requestHead);
            Assert.NotNull(await tokenTask.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedEndpointHasIndependentKeysAndProxiesAcrossManagers(bool copyEndpoint)
    {
        var listenerA = new TcpListener(IPAddress.Loopback, 0);
        var listenerB = new TcpListener(IPAddress.Loopback, 0);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        listenerA.Start();
        listenerB.Start();
        try
        {
            var requestA = ServeOnceAsync(listenerA, timeout.Token);
            var requestB = ServeOnceAsync(listenerB, timeout.Token);
            var endpoint = new ServiceEndpoint(new Uri("http://signalr.invalid"), new StaticTokenCredential());
            using var servicesA = CreateServices(endpoint, new WebProxy($"http://127.0.0.1:{((IPEndPoint)listenerA.LocalEndpoint).Port}"));
            var providerA = servicesA.GetRequiredService<IServiceEndpointManager>().GetEndpointProvider(endpoint);
            var endpointB = copyEndpoint ? new ServiceEndpoint(endpoint) : endpoint;
            using var servicesB = CreateServices(endpointB, new WebProxy($"http://127.0.0.1:{((IPEndPoint)listenerB.LocalEndpoint).Port}"));
            var providerB = servicesB.GetRequiredService<IServiceEndpointManager>().GetEndpointProvider(endpointB);

            // B negotiates first, even though A's provider was constructed first.
            var tokenB = providerB.GenerateClientAccessTokenAsync("hub");
            Assert.Same(requestB, await Task.WhenAny(requestA, requestB).WaitAsync(timeout.Token));
            Assert.StartsWith("GET http://signalr.invalid/api/v1/auth/accessKey ", await requestB);
            Assert.NotNull(await tokenB.WaitAsync(timeout.Token));

            // A has its own key cache and must fetch through A's proxy, even while B's key is fresh.
            Assert.NotSame(providerA.AccessKey, providerB.AccessKey);
            var tokenA = providerA.GenerateClientAccessTokenAsync("hub");
            Assert.StartsWith("GET http://signalr.invalid/api/v1/auth/accessKey ", await requestA);
            Assert.NotNull(await tokenA.WaitAsync(timeout.Token));
        }
        finally
        {
            timeout.Cancel();
            listenerA.Stop();
            listenerB.Stop();
        }
    }

    private static ServiceProvider CreateServices(ServiceEndpoint endpoint, IWebProxy proxy)
    {
        return new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddSignalR().AddAzureSignalR(options =>
            {
                options.Endpoints = new[] { endpoint };
                options.Proxy = proxy;
            }).Services.BuildServiceProvider();
    }

    private static async Task<string> ServeOnceAsync(TcpListener listener, CancellationToken cancellationToken = default)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var stream = client.GetStream();
        var buffer = new byte[4096];
        var head = new StringBuilder();
        while (!head.ToString().Contains("\r\n\r\n"))
        {
            var n = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (n == 0)
            {
                break;
            }
            head.Append(Encoding.ASCII.GetString(buffer, 0, n));
        }
        var body = "{\"KeyId\":\"kid\",\"AccessKey\":\"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789\"}";
        var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
        return head.ToString();
    }

    private sealed class StaticTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => new("t", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) => new(GetToken(requestContext, cancellationToken));
    }
}
