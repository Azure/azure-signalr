// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Azure.SignalR.Tests;

public class EndpointRouterTests
{
    [Fact]
    public void TestDefaultEndpointWeightedRouter()
    {
        const int loops = 1000;
        var context = new RandomContext();
        var drt = new DefaultEndpointRouter();

        const string u1Full = "u1_full", u1Empty = "u1_empty";
        var u1F = GenerateServiceEndpoint(1000, 10, 990, u1Full);
        var u1E = GenerateServiceEndpoint(1000, 10, 0, u1Empty);
        var el = new List<ServiceEndpoint>() { u1E, u1F };
        context.BenchTest(loops, () =>
            drt.GetNegotiateEndpoint(null, el).Name);
        var u1ECount = context.GetCount(u1Empty);
        const int smallVar = 10; 
        Assert.True(u1ECount is > loops - smallVar and <= loops);
        var u1FCount = context.GetCount(u1Full);
        Assert.True(u1FCount <= smallVar);
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