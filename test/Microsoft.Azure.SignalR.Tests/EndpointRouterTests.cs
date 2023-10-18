// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests
{
    public class EndpointRouterTests
    {
        [Fact]
        public void TestDefaultEndpointRouterWeightedMode()
        {
            var drt = GetEndpointRouter(EndpointRoutingMode.Weighted);

            const int loops = 20;
            var context = new RandomContext();

            const string small = "small_instance", large = "large_instance";
            var uSmall = GenerateServiceEndpoint(10, 0, 9, small);
            var uLarge = GenerateServiceEndpoint(1000, 0, 900, large);
            var el = new List<ServiceEndpoint>() { uLarge, uSmall };
            context.BenchTest(loops, () =>
            {
                var ep = drt.GetNegotiateEndpoint(null, el);
                ep.EndpointMetrics.ClientConnectionCount++;
                return ep.Name;
            });
            var uLargeCount = context.GetCount(large);
            const int smallVar = 3;
            var uSmallCount = context.GetCount(small);
            Assert.True(uLargeCount is >= loops - smallVar and <= loops);
            Assert.True(uSmallCount is >= 1 and <= smallVar);
            context.Reset();
        }

        [Fact]
        public void TestDefaultEndpointRouterLeastConnectionMode()
        {
            var drt = GetEndpointRouter(EndpointRoutingMode.LeastConnection);

            const int loops = 10;
            var context = new RandomContext();

            const string small = "small_instance", large = "large_instance";
            var uSmall = GenerateServiceEndpoint(100, 0, 90, small);
            var uLarge = GenerateServiceEndpoint(1000, 0, 200, large);
            var el = new List<ServiceEndpoint>() { uLarge, uSmall };
            context.BenchTest(loops, () =>
            {
                var ep = drt.GetNegotiateEndpoint(null, el);
                ep.EndpointMetrics.ClientConnectionCount++;
                return ep.Name;
            });
            var uLargeCount = context.GetCount(large);
            var uSmallCount = context.GetCount(small);
            Assert.Equal(0, uLargeCount);
            Assert.Equal(10, uSmallCount);
            context.Reset();
        }

        private static IEndpointRouter GetEndpointRouter(EndpointRoutingMode mode)
        {
            var config = new ConfigurationBuilder().Build();
            var serviceProvider = new ServiceCollection()
                .AddSignalR()
                .AddAzureSignalR(
                    o =>
                    {
                        o.EndpointRoutingMode = mode;
                    })
                .Services
                .AddSingleton<IConfiguration>(config)
                .BuildServiceProvider();

            return serviceProvider.GetRequiredService<IEndpointRouter>();
        }

        private static ServiceEndpoint GenerateServiceEndpoint(int capacity, int serverConnectionCount,
            int clientConnectionCount, string name)
        {
            var endpointMetrics = new EndpointMetrics()
            {
                ConnectionCapacity = capacity,
                ClientConnectionCount = clientConnectionCount,
                ServerConnectionCount = serverConnectionCount
            };
            return new ServiceEndpoint("Endpoint=https://url;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789;",
                EndpointType.Primary, name) { EndpointMetrics = endpointMetrics };
        }

        private class RandomContext
        {
            private readonly Dictionary<string, int> _counter = new();

            public void BenchTest(int loops, Func<string> func)
            {
                for (var i = 0; i < loops; i++)
                {
                    var name = func();
                    if (!_counter.ContainsKey(name))
                    {
                        _counter.Add(name, 0);
                    }

                    _counter[name]++;
                }
            }

            public int GetCount(string name)
            {
                return _counter.ContainsKey(name) ? _counter[name] : 0;
            }

            public void Reset()
            {
                _counter.Clear();
            }
        }
    }
}