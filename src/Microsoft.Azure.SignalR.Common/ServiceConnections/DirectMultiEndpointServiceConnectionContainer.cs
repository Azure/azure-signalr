// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// A Multiple endpoints service connection container which sends message directly without router.
    /// <para>Designed for Azure SignalR Function Extension where router functions are hard to pass between different languages. Allows users to specify target endpoints directly and therefore implement routing logic externally.</para>
    /// </summary>
    internal class DirectMultiEndpointServiceConnectionContainer : MultiEndpointServiceConnectionContainerBase
    {
        private readonly IReadOnlyCollection<HubServiceEndpoint> _targetEndpoints;

        public DirectMultiEndpointServiceConnectionContainer(IReadOnlyCollection<HubServiceEndpoint> targetEndpoints, ILoggerFactory loggerFactory) : base(loggerFactory)
        {
            _targetEndpoints = targetEndpoints;
        }

        public override IEnumerable<ServiceEndpoint> GetRoutedEndpoints(ServiceMessage message) => _targetEndpoints;

        public override Task ConnectionInitializedTask => Task.WhenAll(from endpoint in _targetEndpoints select endpoint.ConnectionContainer.ConnectionInitializedTask);

        public override void Dispose()
        {
        }

        #region Not supported method or properties

        public override ServiceConnectionStatus Status => throw new NotSupportedException();

        public override string ServersTag => throw new NotSupportedException();

        public override bool HasClients => throw new NotSupportedException();

        public override Task OfflineAsync(GracefulShutdownMode mode) => throw new NotSupportedException();

        public override Task StartAsync() => throw new NotSupportedException();

        public override Task StartGetServersPing() => throw new NotSupportedException();

        public override Task StopAsync() => throw new NotSupportedException();

        public override Task StopGetServersPing() => throw new NotSupportedException();

        #endregion Not supported method or properties
    }
}