// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class ServiceOptionsSetupFacts
    {
        public const string FakeConnectionString = "Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;Port=8080;Version=1.0";

        private static readonly string[] ConnectionStringKeyPrefixs = new string[] { Constants.Keys.ConnectionStringKeyPrefix, Constants.Keys.ConnectionStringSecondaryKeyPrefix };

        private static readonly Dictionary<string, (string, EndpointType)> EndpointDict = new Dictionary<string, (string, EndpointType)>
        {
            {"a",("a",EndpointType.Primary) },
            {"secondary",("secondary",EndpointType.Primary) },
            {"a:secondary",("a",EndpointType.Secondary) },
            {":secondary",(string.Empty,EndpointType.Secondary) }
        };

        public static IEnumerable<object[]> ParseConnectionStringData = from section in ConnectionStringKeyPrefixs
                                                                        from tuple in EndpointDict
                                                                        select new object[] { section + tuple.Key, tuple.Value.Item1, tuple.Value.Item2 };

        [Theory]
        [MemberData(nameof(ParseConnectionStringData))]
        public void ParseConnectionStringTest(string key, string endpointName, EndpointType type)
        {
            IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            configuration[key] = FakeConnectionString;
            var setup = new ServiceOptionsSetup(configuration);
            var options = new ServiceOptions();
            setup.Configure(options);

            var resultEndpoint = options.Endpoints.Single();
            Assert.Equal(endpointName, resultEndpoint.Name);
            Assert.Equal(type, resultEndpoint.EndpointType);
        }
    }
}