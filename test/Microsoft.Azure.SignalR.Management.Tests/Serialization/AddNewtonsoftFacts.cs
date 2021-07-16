// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class AddNewtonsoftFacts : VerifiableLoggedTest
    {
        public AddNewtonsoftFacts(ITestOutputHelper output) : base(output)
        {
        }

        public static IEnumerable<object[]> SerializdDataSerializerSettings
        {
            get
            {
                yield return new object[] { new HubMethodArgument(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore } };
                yield return new object[] { new HubMethodArgument { Version = new(1, 1, 1) }, new JsonSerializerSettings { Converters = new JsonConverter[] { new VersionConverter() } } };
            }
        }

        #region Transient

        [Fact]
        public async Task TestTransientDefaultSerializationBehaviour()
        {
            var methodName = "send";
            var message = "abc";
            // By default, no camelCase.
            var expectedHttpBody = "{\"Target\":\"send\",\"Arguments\":[\"abc\"]}";
            var serviceHubContext = await CreateTransientHubContextBuilder(expectedHttpBody)
                .CreateAsync("hub", default);

            await serviceHubContext.Clients.All.SendAsync(methodName, message);
            await serviceHubContext.DisposeAsync();
        }

        [Fact]
        public async Task TestTransientModeAddNewtonsoftWithoutAction()
        {
            var methodName = "send";
            var messageContent = "abc";
            // camelCase is used.
            var expectedHttpBody = "{\"target\":\"send\",\"arguments\":[\"abc\"]}";
            var serviceHubContext = await CreateTransientHubContextBuilder(expectedHttpBody)
                .WithNewtonsoftJsonHubProtocol()
                .CreateAsync("hub", default);

            await serviceHubContext.Clients.All.SendAsync(methodName, messageContent);
            await serviceHubContext.DisposeAsync();
        }

        [Theory]
        [MemberData(nameof(SerializdDataSerializerSettings))]
        public async Task TestTransientModeAddNewtonsoftWithAction(HubMethodArgument message, JsonSerializerSettings jsonSerializerSettings)
        {
            var methodName = "send";
            var payloadMessage = new PayloadMessage { Target = methodName, Arguments = new[] { message } };
            var expectedHttpBody = JsonConvert.SerializeObject(payloadMessage, jsonSerializerSettings);

            var serviceHubContext = await CreateTransientHubContextBuilder(expectedHttpBody)
                 .WithNewtonsoftJsonHubProtocol(o => o.PayloadSerializerSettings = jsonSerializerSettings)
                 .CreateAsync("hub", default);

            await serviceHubContext.Clients.All.SendAsync(methodName, message);
            await serviceHubContext.DisposeAsync();
        }

        private static ServiceHubContextBuilder CreateTransientHubContextBuilder(string expectedHttpBody)
        {
            return new ServiceHubContextBuilder()
                .WithOptions(o =>
                {
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                    o.ServiceTransportType = ServiceTransportType.Transient;
                })
                .ConfigureServices(services =>
                {
                    services.AddHttpClient(string.Empty).AddHttpMessageHandler(() => new TestRootHandler((message, cancellationToken) =>
                    {
                        var actualBody = message.Content.ReadAsStringAsync().Result;
                        Assert.Equal(expectedHttpBody, actualBody);
                    }));
                });
        }

        #endregion Transient

        #region Persistent

        [Fact]
        public async Task TestPersistentModeWithDefaultConfiguration()
        {
            ServiceHubContextImpl serviceHubContext = null;
            try
            {
                serviceHubContext = (ServiceHubContextImpl)await CreatePersistentServiceHubContextBuilder(out _)
                .CreateAsync("hubName", default);
                var hubProtocol = serviceHubContext.ServiceProvider.GetRequiredService<IHubProtocol>();

                //default is System.Text.Json
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
                serviceHubContext = (ServiceHubContextImpl)await CreatePersistentServiceHubContextBuilder(out var connectionFactory)
                    .WithNewtonsoftJsonHubProtocol()
                    .CreateAsync("hubName", default);
                await serviceHubContext.Clients.All.SendAsync(methodName, message);

                var sentMessage = connectionFactory.CreatedConnections.Single().Value.SelectMany(conn => ((TestServiceConnection)conn).ReceivedMessages).Single() as BroadcastDataMessage;
                var protocol = new NewtonsoftJsonHubProtocol();
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
            using (StartLog(out var loggerFactory, nameof(TestPersistentModeAddNewtonsoftWithAction)))
            {
                var logger = loggerFactory.CreateLogger("");
                var methodName = "send";
                ServiceHubContextImpl serviceHubContext = null;
                try
                {
                    serviceHubContext = await CreatePersistentServiceHubContextBuilder(out var connectionFactory)
                    .WithNewtonsoftJsonHubProtocol(o => o.PayloadSerializerSettings = jsonSerializerSettings)
                    .CreateAsync("hubName", default) as ServiceHubContextImpl;
                    await serviceHubContext.Clients.All.SendAsync(methodName, message);

                    var sentMessage = connectionFactory.CreatedConnections.Single().Value.SelectMany(conn => ((TestServiceConnection)conn).ReceivedMessages).Single() as BroadcastDataMessage;
                    var protocol = new NewtonsoftJsonHubProtocol(Options.Create(new NewtonsoftJsonHubProtocolOptions() { PayloadSerializerSettings = jsonSerializerSettings }));
                    var exptectedPayload = protocol.GetMessageBytes(new InvocationMessage(methodName, new object[] { message }));
                    var actualPayload = sentMessage.Payloads[protocol.Name];
                    logger.LogInformation(JsonConvert.SerializeObject(jsonSerializerSettings));
                    logger.LogInformation("Expected:  {expected}", Encoding.UTF8.GetString(exptectedPayload.Span));
                    logger.LogInformation("Actual:    {actual}", Encoding.UTF8.GetString(actualPayload.Span));
                    logger.LogInformation("Version:   {version}", Environment.Version.ToString());

                    var hubProtocol = serviceHubContext.ServiceProvider.GetRequiredService<IHubProtocol>();
                    logger.LogInformation("Current HubProtocol:  {hubProtocol}", hubProtocol.GetType().FullName);
                    var services = serviceHubContext.ServiceProvider.GetRequiredService<IReadOnlyCollection<ServiceDescriptor>>();
                    foreach (var service in services.Where(s => s.ServiceType == typeof(IHubProtocol)))
                    {
                        logger.LogInformation("Added HubProtocol:{impl}", service.ImplementationType);
                    }

                    Assert.True(exptectedPayload.Span.SequenceEqual(actualPayload.Span));
                }
                finally
                {
                    await serviceHubContext?.DisposeAsync();
                }
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

        public class HubMethodArgument
        {
            public string Content { get; set; }
            public Version Version { get; set; }
        }
    }
}