﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Azure.SignalR.IntegrationTests.MockService;

namespace Microsoft.Azure.SignalR.IntegrationTests.Infrastructure
{
    internal class MockServiceConnectionContext : ConnectionContext
    {
        public override string ConnectionId { get; set; }
        public override IFeatureCollection Features { get; }
        public override IDictionary<object, object> Items { get; set; }
        public override IDuplexPipe Transport { get; set; }

        IMockService _mockService;
        MockServiceSideConnection _svcSideConnection;

        public MockServiceConnectionContext(IMockService mockService, HubServiceEndpoint endpoint, string target, string id)
        {
            ConnectionId = id;
            Features = new FeatureCollection();
            Items = new ConcurrentDictionary<object, object>();

            var duplexPipePair = DuplexPipe.CreateConnectionPair(PipeOptions.Default, PipeOptions.Default);

            // SDK's side uses Transport property to read/write to our mock service
            Transport = duplexPipePair.Transport;

            _mockService = mockService;
            _svcSideConnection = _mockService.RegisterSDKConnectionContext(this, endpoint, target, duplexPipePair.Application);
        }

        public override async ValueTask DisposeAsync()
        {
            await _mockService.StopConnectionAsync(_svcSideConnection);
        }

        public MockServiceConnection MyMockServiceConnetion { get; set; }
    }
}