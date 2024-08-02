// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

public class TraceManagerLoggerProviderTest
{
    /// <summary>
    /// TraceManagerLoggerProvider throws when its CreateLogger returns TraceSourceLogger when using HttpConnections.Client 1.0.0
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task TestTraceManagerLoggerProviderCanDisposeHttpConnection()
    {
        var lf = new LoggerFactory();
        lf.AddProvider(new TraceManagerLoggerProvider(new TraceManager()));
        await StartAsync(lf);
    }

    private static async Task StartAsync(ILoggerFactory lf)
    {
        var connection = await ConnectAsync(lf);
        // var connection = Connect(lf);
        await ((HttpConnection)connection).DisposeAsync();
    }

    public static async Task<ConnectionContext> ConnectAsync(ILoggerFactory lf)
    {
        // Await to enforce it run in another thread
        await Task.Yield();
        var httpConnectionOptions = new HttpConnectionOptions
        {
            Url = new Uri("http://locolhost"),
        };

        return new HttpConnection(httpConnectionOptions, lf);
    }
}