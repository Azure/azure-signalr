// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.Azure.SignalR.Tests;

internal class TestConnection : ConnectionContext, IClientConnection
{
    public override string ConnectionId { get; set; }

    public override IFeatureCollection Features { get; }

    public override IDictionary<object, object> Items { get; set; }

    public override IDuplexPipe Transport { get; set; }

    public IDuplexPipe Application { get; set; }

    public string Target { get; set; }

    public string InstanceId => throw new NotImplementedException();

    public IServiceConnection ServiceConnection => throw new NotImplementedException();

    public string HubProtocol => throw new NotImplementedException();

    public TestConnection()
    {
        Features = new FeatureCollection();
        Items = new ConcurrentDictionary<object, object>();

        var pipeOptions = new PipeOptions();
        var pair = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);
        Transport = pair.Transport;
        Application = pair.Application;
    }
}
