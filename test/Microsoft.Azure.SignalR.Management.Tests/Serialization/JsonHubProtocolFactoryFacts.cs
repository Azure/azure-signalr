// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Azure.Core.Serialization;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class JsonHubProtocolFactoryFacts
    {
        [Fact]
        public void TestGetProtocolWithObjectSerializer()
        {
            var serviceManagerOptions = Options.Create(new ServiceManagerOptions
            {
                ObjectSerializer = new JsonObjectSerializer()
            });
            var factory = new JsonHubProtocolFactory(serviceManagerOptions);
            var jsonProtocol = factory.GetJsonHubProtocol();
            Assert.IsType<JsonObjectSerializerHubProtocol>(jsonProtocol);
        }

        [Fact]
        public void TestGetProtocolWithoutObjectSerializer()
        {
            var serviceManagerOptions = Options.Create(new ServiceManagerOptions());
            var factory = new JsonHubProtocolFactory(serviceManagerOptions);
            var jsonProtocol = factory.GetJsonHubProtocol();
            Assert.IsType<JsonHubProtocol>(jsonProtocol);
        }
    }
}
