// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Azure.Core.Serialization;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Management.Tests
{
    public class JsonHubProtocolResolverFacts
    {
        private readonly ILoggerFactory _loggerFactory;
        public JsonHubProtocolResolverFacts(ITestOutputHelper testOutputHelper)
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddXunit(testOutputHelper);
        }

        [Fact]
        public void TestProtocolResolveWithObjectSerializer()
        {
            var serviceManagerOptions = Options.Create(new ServiceManagerOptions
            {
                ObjectSerializer = new JsonObjectSerializer()
            });
            var hubProtocols = new IHubProtocol[] { new JsonHubProtocol(), new JsonObjectSerializerHubProtocol(serviceManagerOptions) };
            var resolver = new JsonHubProtocolResolver(hubProtocols, _loggerFactory.CreateLogger<JsonHubProtocolResolver>());

            var jsonProtocol = resolver.GetProtocol("json", null);
            Assert.IsType<JsonObjectSerializerHubProtocol>(jsonProtocol);
        }

        [Fact]
        public void TestProtocolResolveWithoutObjectSerializer()
        {
            var serviceManagerOptions = Options.Create(new ServiceManagerOptions());
            var hubProtocols = new IHubProtocol[] { new JsonHubProtocol(), new JsonObjectSerializerHubProtocol(serviceManagerOptions) };
            var resolver = new JsonHubProtocolResolver(hubProtocols, _loggerFactory.CreateLogger<JsonHubProtocolResolver>());

            var jsonProtocol = resolver.GetProtocol("json", null);
            Assert.IsType<JsonHubProtocol>(jsonProtocol);
        }
    }
}
