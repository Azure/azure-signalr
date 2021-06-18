// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.NetCore21.Tests
{
    public class AddNewtonsoftFacts
    {
        public static IEnumerable<object[]> SerializdDataSerializerSettings
        {
            get
            {
                yield return new object[] { new HubMethodArgument(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore } };
                yield return new object[] { new HubMethodArgument { Version = new(1, 1, 1) }, new JsonSerializerSettings { Converters = new JsonConverter[] { new VersionConverter() } } };
            }
        }

        #region Persistent

        [Fact]
        public async Task TestPersistentModeWithSerializationBehaviour()
        {
            ServiceHubContextImpl serviceHubContext = null;
            try
            {
                serviceHubContext = await CreatePersistentServiceHubContextBuilder(out _)
                .CreateAsync("hubName", default) as ServiceHubContextImpl;
                var hubProtocol = serviceHubContext.ServiceProvider.GetRequiredService<IHubProtocol>();

                //Default is JsonHubProtocol. JsonHubProtocol uses Newtonsoft.Json below .Net core 3.0
                Assert.IsType<JsonHubProtocol>(hubProtocol);
            }
            finally
            {
                await serviceHubContext?.DisposeAsync();
            }
        }

        [Fact]
        public async Task TestPersistentModeAddNewtonsoftWithoutAction()
        {
            ServiceHubContextImpl serviceHubContext = null;
            try
            {
                var methodName = "send";
                var message = "abc";
                serviceHubContext = await CreatePersistentServiceHubContextBuilder(out var connectionFactory)
                    .WithNewtonsoftJsonHubProtocol()
                    .CreateAsync("hubName", default) as ServiceHubContextImpl;
                await serviceHubContext.Clients.All.SendAsync(methodName, message);

                var sentMessage = connectionFactory.CreatedConnections.Single().Value.SelectMany(conn => ((TestServiceConnection)conn).ReceivedMessages).Single() as BroadcastDataMessage;
                var protocol = new JsonHubProtocol();
                var exptectedPayload = protocol.GetMessageBytes(new InvocationMessage(methodName, new object[] { message }));
                var actualPayload = sentMessage.Payloads[protocol.Name];
                Assert.True(exptectedPayload.Span.SequenceEqual(actualPayload.Span));
            }
            finally
            {
                await serviceHubContext?.DisposeAsync();
            }
        }

        [Theory]
        [MemberData(nameof(SerializdDataSerializerSettings))]
        public async Task TestPersistentModeAddNewtonsoftWithAction(HubMethodArgument message, JsonSerializerSettings jsonSerializerSettings)
        {
            ServiceHubContextImpl serviceHubContext = null;
            try
            {
                var methodName = "send";
                serviceHubContext = await CreatePersistentServiceHubContextBuilder(out var connectionFactory)
                    .WithNewtonsoftJsonHubProtocol(o => o.PayloadSerializerSettings = jsonSerializerSettings)
                    .CreateAsync("hubName", default) as ServiceHubContextImpl;
                await serviceHubContext.Clients.All.SendAsync(methodName, message);

                var sentMessage = connectionFactory.CreatedConnections.Single().Value.SelectMany(conn => ((TestServiceConnection)conn).ReceivedMessages).Single() as BroadcastDataMessage;
                var protocol = new JsonHubProtocol(Options.Create(new JsonHubProtocolOptions() { PayloadSerializerSettings = jsonSerializerSettings }));
                var exptectedPayload = protocol.GetMessageBytes(new InvocationMessage(methodName, new object[] { message }));
                var actualPayload = sentMessage.Payloads[protocol.Name];

                Assert.True(exptectedPayload.Span.SequenceEqual(actualPayload.Span));
            }
            finally
            {
                await serviceHubContext?.DisposeAsync();
            }
        }

        private static ServiceHubContextBuilder CreatePersistentServiceHubContextBuilder(out TestServiceConnectionFactory connectionFactory)
        {
            connectionFactory = new TestServiceConnectionFactory();
            var connectionFactoryAlias = connectionFactory;
            //skip check if endpoint online
            var routerMock = new Mock<IEndpointRouter>();
            routerMock.Setup(r => r.GetEndpointsForBroadcast(It.IsAny<IEnumerable<ServiceEndpoint>>())).Returns((IEnumerable<ServiceEndpoint> es) => es);
            return new ServiceHubContextBuilder()
                .WithOptions(o =>
                {
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                })
                .WithRouter(routerMock.Object)
                .ConfigureServices(services => services.AddSingleton<IServiceConnectionFactory>(connectionFactoryAlias));
        }

        #endregion Persistent

        #region Transient

        // Transient mode behaviours are the same between .NetCore 2.1 and above, so tests under .Net 5.0 are enough

        #endregion Transient

        public sealed class HubMethodArgument
        {
            public string Content { get; set; }
            public Version Version { get; set; }
        }
    }
}