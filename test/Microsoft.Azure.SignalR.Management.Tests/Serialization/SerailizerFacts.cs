// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class SerailizerFacts
    {
        public static IEnumerable<object[]> IgnoreNullObjectSerializers
        {
            get
            {
                yield return new object[] { new JsonObjectSerializer(new() { IgnoreNullValues = true }) };
                yield return new object[] { new NewtonsoftJsonObjectSerializer(new() { NullValueHandling = NullValueHandling.Ignore }) };
            }
        }
        private static readonly string TargetName = "target";
        private static readonly object Argument = new
        {
            Content = default(object), // test null value handling
        };
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        public SerailizerFacts(ITestOutputHelper testOutput)
        {
            _loggerFactory = new LoggerFactory().AddXunit(testOutput);
            _logger = _loggerFactory.CreateLogger<SerailizerFacts>();
        }

        #region persistent mode
        [Fact]
        public async Task TestPersistent_DefaultSerialization_Behaviour()
        {
            var message = new InvocationMessage(TargetName, new object[] { Argument });
            var expectedHubProtocol = new JsonHubProtocol();
            var expectedPayload = expectedHubProtocol.GetMessageBytes(message);
            using var serviceHubContext = await CreatePersistentBuilder(expectedPayload)
                .BuildServiceManager()
                .CreateHubContextAsync("hubName", default);
            await serviceHubContext.Clients.All.SendAsync(TargetName, Argument);
        }

        [Theory]
        [MemberData(nameof(IgnoreNullObjectSerializers))]
        public async Task TestPersistent_ObjectSerializer(ObjectSerializer objectSerializer)
        {
            var message = new InvocationMessage(TargetName, new object[] { Argument });
            var expectedHubProtocol = new JsonObjectSerializerHubProtocol(objectSerializer);
            var expectedPayload = expectedHubProtocol.GetMessageBytes(message);
            using var serviceHubContext = await CreatePersistentBuilder(expectedPayload)
                .WithOptions(o => o.ObjectSerializer = objectSerializer)
                .BuildServiceManager()
                .CreateHubContextAsync("hubName", default);
            await serviceHubContext.Clients.All.SendAsync(TargetName, Argument);

            var originalProtocol = new JsonHubProtocol();
            var originalPayload = originalProtocol.GetMessageBytes(message);
            // Verify that the result is customized compared to default settings.
            Assert.False(expectedPayload.Span.SequenceEqual(originalPayload.Span));
        }

        private ServiceManagerBuilder CreatePersistentBuilder(ReadOnlyMemory<byte> expectedPayload)
        {

            var mockConnectionContainer = new Mock<IServiceConnectionContainer>();
            mockConnectionContainer.Setup(c => c.WriteAsync(It.IsAny<BroadcastDataMessage>()))
                .Callback<ServiceMessage>(message =>
                {
                    var m = message as BroadcastDataMessage;
                    var actualPayload = m.Payloads["json"];
                    _logger.LogDebug($"Expected: {Encoding.UTF8.GetString(expectedPayload.Span)}");
                    _logger.LogDebug($"Actual: {Encoding.UTF8.GetString(actualPayload.Span)}");
                    Assert.True(actualPayload.Span.SequenceEqual(expectedPayload.Span));
                });
            return new ServiceManagerBuilder()
                .WithOptions(o =>
                {
                    o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single();
                    o.ServiceTransportType = ServiceTransportType.Persistent;
                })
                .WithLoggerFactory(_loggerFactory)
                .ConfigureServices(services => services.AddSingleton(mockConnectionContainer.Object));
        }
        #endregion
    }
}
