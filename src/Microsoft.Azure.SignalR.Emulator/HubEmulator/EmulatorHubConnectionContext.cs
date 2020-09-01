// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal class EmulatorHubConnectionContext : HubConnectionContext
    {
        public ConnectionContext ConnectionContext { get; }

        public EmulatorHubConnectionContext(string hub, ConnectionContext connectionContext, HubConnectionContextOptions contextOptions, ILoggerFactory loggerFactory) : base(connectionContext, contextOptions, loggerFactory)
        {
            ConnectionContext = connectionContext ?? throw new ArgumentNullException(nameof(connectionContext));
            var upstreamProperties = new HttpUpstreamPropertiesFeature(connectionContext, hub);
            Features.Set<IHttpUpstreamPropertiesFeature>(upstreamProperties);
        }
    }
}
