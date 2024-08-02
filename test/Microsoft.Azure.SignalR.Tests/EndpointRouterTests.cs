// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class EndpointRouterTests
{
    [Fact]
    public void TestDefaultEndpointRouterWeightedMode()
    {
        var drt = new DefaultEndpointRouter();

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

    [Theory]
    [InlineData(200)]
    [InlineData(300)]
    [InlineData(400)]
    [InlineData(500)]
    public void TestDefaultEndpointRouterWeightedModeWhenAutoScaleIsEnabled(int quotaOfScaleUpInstance)
    {
        var drt = new DefaultEndpointRouter();

        var loops = 100 + (quotaOfScaleUpInstance / 5);
        var context = new RandomContext();
        const double quotaBarForScaleUp = 0.8;

        var endpointA = GenerateServiceEndpoint(quotaOfScaleUpInstance, 0, 80, "a");
        var endpointB = GenerateServiceEndpoint(100, 0, 70, "b");
        var endpointC = GenerateServiceEndpoint(100, 0, 70, "c");
        var el = new List<ServiceEndpoint>() {endpointA, endpointB, endpointC};
        context.BenchTest(loops, () =>
        {
            var ep = drt.GetNegotiateEndpoint(null, el);
            ep.EndpointMetrics.ClientConnectionCount++;
            var percent = (ep.EndpointMetrics.ClientConnectionCount + ep.EndpointMetrics.ServerConnectionCount) /
                          (double)ep.EndpointMetrics.ConnectionCapacity;
            if (percent > quotaBarForScaleUp)
            {
                ep.EndpointMetrics.ConnectionCapacity += 100;
            }

            return ep.Name;
        });

        Assert.Equal(context.GetCount("a") + context.GetCount("b") + context.GetCount("c"), loops);
        Assert.Equal(quotaOfScaleUpInstance, endpointA.EndpointMetrics.ConnectionCapacity);
        Assert.Equal(200, endpointB.EndpointMetrics.ConnectionCapacity);
        Assert.Equal(200, endpointC.EndpointMetrics.ConnectionCapacity);

        context.Reset();
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