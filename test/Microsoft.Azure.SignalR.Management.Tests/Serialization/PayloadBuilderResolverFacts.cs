// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Core.Serialization;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Tests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class PayloadBuilderResolverFacts
    {
        public static IEnumerable<object[]> DefaultProtocolNotChangedTestData()
        {
            yield return new object[] { (ServiceManagerBuilder b) => { } };

#pragma warning disable CS0618 // Type or member is obsolete
            yield return new object[] { (ServiceManagerBuilder b) => { b.WithOptions(o => o.JsonSerializerSettings.DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.IgnoreAndPopulate); } };
#pragma warning restore CS0618 // Type or member is obsolete

            yield return new object[] { (ServiceManagerBuilder b) => { b.WithNewtonsoftJson(); } };

            yield return new object[] { (ServiceManagerBuilder b) => { b.WithOptions(o => o.UseJsonObjectSerializer(new JsonObjectSerializer())); } };
        }

        [Theory]
        [MemberData(nameof(DefaultProtocolNotChangedTestData))]
        public async Task DefaultProtocolNotChangedTest(Action<ServiceManagerBuilder> configure)
        {
            var builder = new ServiceManagerBuilder()
                .WithOptions(o => o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single());
            configure(builder);
            var serviceHubContext = await builder.BuildServiceManager()
                .CreateHubContextAsync("hub", default);
            var payloadBuilderResolver = (serviceHubContext as ServiceHubContextImpl).ServiceProvider.GetRequiredService<PayloadBuilderResolver>();
            Assert.IsType<JsonPayloadContentBuilder>(payloadBuilderResolver.GetPayloadContentBuilder());
        }

        public static IEnumerable<object[]> DefaultProtocolChangedTestData()
        {
            yield return new object[] { (ServiceManagerBuilder b) => { b.AddHubProtocol(new MessagePackHubProtocol()); } };
            yield return new object[] { (ServiceManagerBuilder b) => { b.AddHubProtocol(new JsonHubProtocol()); } };
            yield return new object[] { (ServiceManagerBuilder b) => { b.WithHubProtocols(new MessagePackHubProtocol()); } };
            yield return new object[] { (ServiceManagerBuilder b) => { b.WithHubProtocols(new MessagePackHubProtocol()).WithNewtonsoftJson(); } };
            yield return new object[] { (ServiceManagerBuilder b) => { b.WithHubProtocols(new MessagePackHubProtocol(), new JsonHubProtocol()); } };
        }

        [Theory]
        [MemberData(nameof(DefaultProtocolChangedTestData))]
        public async Task DefaultProtocolChangedTest(Action<ServiceManagerBuilder> configure)
        {
            var builder = new ServiceManagerBuilder()
                .WithOptions(o => o.ConnectionString = FakeEndpointUtils.GetFakeConnectionString(1).Single());
            configure(builder);
            var serviceHubContext = await builder.BuildServiceManager()
                .CreateHubContextAsync("hub", default);
            var payloadBuilderResolver = (serviceHubContext as ServiceHubContextImpl).ServiceProvider.GetRequiredService<PayloadBuilderResolver>();
            Assert.IsType<BinaryPayloadContentBuilder>(payloadBuilderResolver.GetPayloadContentBuilder());
        }
    }
}
