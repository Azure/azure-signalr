// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Security.Claims;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.Azure.SignalR
{
    public class ServiceConnectionContext : ConnectionContext,
                                            IConnectionUserFeature,
                                            IConnectionItemsFeature,
                                            IConnectionIdFeature,
                                            IConnectionTransportFeature
    {
        public ServiceConnectionContext(ServiceMessage serviceMessage)
        {
            ConnectionId = serviceMessage.GetConnectionId();
            ProtocolName = serviceMessage.GetProtocol();

            // Create the Duplix Pipeline for the virtual connection
            var options = new HttpConnectionOptions();
            var transportPipeOptions = new PipeOptions(pauseWriterThreshold: options.TransportMaxBufferSize,
                resumeWriterThreshold: options.TransportMaxBufferSize / 2,
                readerScheduler: PipeScheduler.ThreadPool,
                useSynchronizationContext: false);
            var appPipeOptions = new PipeOptions(pauseWriterThreshold: options.ApplicationMaxBufferSize,
                resumeWriterThreshold: options.ApplicationMaxBufferSize / 2,
                readerScheduler: PipeScheduler.ThreadPool,
                useSynchronizationContext: false);
            var pair = DuplexPipe.CreateConnectionPair(transportPipeOptions, appPipeOptions);
            Transport = pair.Application;
            Application = pair.Transport;

            Features = new FeatureCollection();
            // Disable Ping for this virtual connection, set any TimeSpan is OK.
            Features.Set<IConnectionInherentKeepAliveFeature>(new ConnectionInherentKeepAliveFeature(TimeSpan.FromSeconds(90)));
            Features.Set<IConnectionUserFeature>(this);
            Features.Set<IConnectionItemsFeature>(this);
            Features.Set<IConnectionIdFeature>(this);
            Features.Set<IConnectionTransportFeature>(this);
        }

        public override string ConnectionId { get; set; }

        public override IFeatureCollection Features { get;}

        public override IDictionary<object, object> Items { get; set; } = new ConnectionItems(new ConcurrentDictionary<object, object>());

        public override IDuplexPipe Transport { get; set; }

        public IDuplexPipe Application { get; set; }

        public string ProtocolName { get; set; }

        public ClaimsPrincipal User { get ; set; }

    }
}
