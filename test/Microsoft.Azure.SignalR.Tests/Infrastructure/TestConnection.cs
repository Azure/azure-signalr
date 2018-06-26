// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.Azure.SignalR.Tests
{
    public class TestConnection : ConnectionContext, IDisposable
    {
        private readonly DuplexPipe.DuplexPipePair _pair;

        public TestConnection()
        {
            Features = new FeatureCollection();
            Items = new ConcurrentDictionary<object, object>();

            var pipeOptions = new PipeOptions();
            _pair = DuplexPipe.CreateConnectionPair(pipeOptions, pipeOptions);
            Transport = _pair.Transport;
            Application = _pair.Application;
        }

        public override string ConnectionId { get; set; }

        public override IFeatureCollection Features { get; }

        public override IDictionary<object, object> Items { get; set; }

        public override IDuplexPipe Transport { get; set; }

        public IDuplexPipe Application { get; set; }

        // To simulate the reconnection after disposed.
        public void SafeReconnect()
        {
            if (Transport == null)
            {
                Transport = _pair.Transport;
            }
        }

        public void Dispose()
        {
            Transport = null;
        }
    }
}
