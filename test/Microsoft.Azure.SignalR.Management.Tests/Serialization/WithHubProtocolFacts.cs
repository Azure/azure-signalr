// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using MessagePack;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class WithHubProtocolFacts
    {
        private static readonly JsonHubProtocol Json = new JsonHubProtocol(Options.Create(new JsonHubProtocolOptions()
        {
            PayloadSerializerOptions = new() { WriteIndented = true }
        }));
        private static readonly MessagePackHubProtocol MessagePack = new MessagePackHubProtocol();
        public static IEnumerable<object[]> AddProtocolTestData()
        {
            yield return new object[] { Json };
            yield return new object[] { MessagePack };
            yield return new object[] { MessagePack, Json };
        }

        [Theory]
        [MemberData(nameof(AddProtocolTestData))]
        public async Task PersistentWithProtocolTest(params IHubProtocol[] hubProtocols)
        {
            var mockConnectionContainer = new Mock<IServiceConnectionContainer>();
            mockConnectionContainer.Setup(c => c.WriteAsync(It.IsAny<BroadcastDataMessage>()))
                .Callback<ServiceMessage>(message =>
                {
                    var m = message as BroadcastDataMessage;
                    Assert.Equal(hubProtocols.Length, m.Payloads.Count);
                    foreach (var hubProtocol in hubProtocols)
                    {
                        var expectedMessageBytes = hubProtocol.GetMessageBytes(new InvocationMessage("target", new object[] { "argument" }));
                        Assert.True(expectedMessageBytes.Span.SequenceEqual(m.Payloads[hubProtocol.Name].Span));
                    }
                });
            var hubContext = await new ServiceManagerBuilder()
                    .WithOptions(o =>
                    {
                        o.ConnectionString = "Endpoint=http://localhost;Port=8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";
                        o.ServiceTransportType = ServiceTransportType.Persistent;
                    })
                    .WithHubProtocols(hubProtocols)
                    .ConfigureServices(services => services.AddSingleton(mockConnectionContainer.Object))
                    .BuildServiceManager()
                    .CreateHubContextAsync("hub", default);
            await hubContext.Clients.All.SendAsync("target", "argument");
        }

        [Fact]
        public async Task PersistentAddMessagePackTest()
        {
            var mockConnectionContainer = new Mock<IServiceConnectionContainer>();
            var expectedHubProtocols = new IHubProtocol[] { new JsonHubProtocol(), new MessagePackHubProtocol() };
            mockConnectionContainer.Setup(c => c.WriteAsync(It.IsAny<BroadcastDataMessage>()))
                .Callback<ServiceMessage>(message =>
                {
                    var m = message as BroadcastDataMessage;
                    Assert.Equal(2, m.Payloads.Count);
                    foreach (var hubProtocol in expectedHubProtocols)
                    {
                        var expectedMessageBytes = hubProtocol.GetMessageBytes(new InvocationMessage("target", new object[] { "argument" }));
                        Assert.True(expectedMessageBytes.Span.SequenceEqual(m.Payloads[hubProtocol.Name].Span));
                    }
                });
            var hubContext = await new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ConnectionString = "Endpoint=http://localhost;Port=8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                })
                .AddHubProtocol(new MessagePackHubProtocol())
                .ConfigureServices(services => services.AddSingleton(mockConnectionContainer.Object))
                .BuildServiceManager()
                .CreateHubContextAsync("hub", default);
            await hubContext.Clients.All.SendAsync("target", "argument");
        }

        [Fact]
        public async Task PersistentAddMessagePack_ThenWithNewtonsoftTest()
        {
            var hubContext = await new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ConnectionString = "Endpoint=http://localhost;Port=8080;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Version=1.0;";
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                })
                .AddHubProtocol(new MessagePackHubProtocol())
                .WithNewtonsoftJson()
                .BuildServiceManager()
                .CreateHubContextAsync("hub", default);
            var allProtocols = (hubContext as ServiceHubContextImpl).ServiceProvider.GetRequiredService<IHubProtocolResolver>().AllProtocols;
            Assert.Equal(2, allProtocols.Count);
            Assert.Contains(allProtocols, p => p is MessagePackHubProtocol);
            Assert.IsType<NewtonsoftJsonObjectSerializer>((allProtocols.First(p => p is JsonObjectSerializerHubProtocol) as JsonObjectSerializerHubProtocol).ObjectSerializer);
        }

        [Fact]
        public async Task TransientWithHubProtocolTest()
        {
            var invocationMessage = new InvocationMessage("target", new object[] { "a", new[] { 1, 2 }, null });
            var hubProtocols = new IHubProtocol[] { new MessagePackHubProtocol(), new JsonHubProtocol() };
            var hubContext = await new ServiceManagerBuilder()
            .WithOptions(o =>
            {
                o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                o.ServiceTransportType = ServiceTransportType.Transient;
            })
            .ConfigureServices(services =>
            {
                services.AddHttpClient(string.Empty).AddHttpMessageHandler(() => new TestRootHandler((message, cancellationToken) =>
                {
                    var reader = new MessagePackReader(message.Content.ReadAsByteArrayAsync(cancellationToken).Result);
                    Assert.Equal(2, reader.ReadMapHeader());
                    Assert.Equal(hubProtocols[0].Name, reader.ReadString());
                    Assert.True(hubProtocols[0].GetMessageBytes(invocationMessage).Span.SequenceEqual(reader.ReadBytes().Value.ToArray().AsSpan()));
                    Assert.Equal(hubProtocols[1].Name, reader.ReadString());
                    Assert.True(hubProtocols[1].GetMessageBytes(invocationMessage).Span.SequenceEqual(reader.ReadBytes().Value.ToArray().AsSpan()));
                    Assert.True(reader.End);
                }));
            })
            .WithHubProtocols(hubProtocols)
            .BuildServiceManager()
            .CreateHubContextAsync("hub", default);
        }

        [Fact]
        public void ForbidOtherCustomProtocolTest()
        {
            var customProtocol1 = Mock.Of<IHubProtocol>(h => h.Name == "abc");
            var customProtocol2 = Mock.Of<IHubProtocol>(h => h.Name == "cde");
            Assert.Throws<ArgumentException>(() => new ServiceManagerBuilder().WithHubProtocols(customProtocol1, customProtocol2));
            Assert.Throws<ArgumentException>(() => new ServiceManagerBuilder().AddHubProtocol(customProtocol1));
        }
    }
}
