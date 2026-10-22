// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using Xunit;

namespace Microsoft.Azure.SignalR.Emulator.Tests;

public class HttpUpstreamTriggerTests
{
    [Theory]
    [InlineData("user", "name: John", "message")]
    [InlineData("non-áščíí-value", "name: Ján Neščivera 好人", "你好")]
    public async Task UpstreamPreservesUserHeaders(string userId, string claim, string eventName)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        string receivedUserId = null;
        string receivedClaim = null;
        string receivedEvent = null;
        using var upstream = new WebHostBuilder()
            .UseKestrel()
            .UseUrls("http://127.0.0.1:0")
            .Configure(app => app.Run(context =>
            {
                receivedUserId = context.Request.Headers[Constants.Headers.AsrsUserId];
                receivedClaim = context.Request.Headers[Constants.Headers.AsrsUserClaims];
                receivedEvent = context.Request.Headers[Constants.Headers.AsrsEvent];
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            }))
            .Build();
        await upstream.StartAsync(timeout.Token);

        var services = new ServiceCollection();
        new Startup(new ConfigurationBuilder().Build()).ConfigureServices(services);
        using var provider = services.BuildServiceProvider();
        var trigger = provider.GetRequiredService<IHttpUpstreamTrigger>();
        var properties = new Mock<IHttpUpstreamPropertiesFeature>();
        properties.SetupGet(p => p.ConnectionId).Returns("connection");
        properties.SetupGet(p => p.UserIdentifier).Returns(userId);
        properties.SetupGet(p => p.ClaimStrings).Returns(new[] { claim });
        var uri = new Uri(upstream.ServerFeatures.Get<IServerAddressesFeature>().Addresses.Single());

        using var response = await trigger.TriggerAsync(
            new UpstreamContext { Uri = uri },
            properties.Object,
            new InvokeUpstreamParameters("hub", "messages", eventName),
            token: timeout.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(userId, receivedUserId);
        Assert.Equal(claim, receivedClaim);
        Assert.Equal(eventName, receivedEvent);
        await upstream.StopAsync(timeout.Token);
    }
}
