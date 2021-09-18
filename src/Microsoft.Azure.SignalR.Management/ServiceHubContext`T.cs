// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
#if NETCOREAPP2_1_OR_GREATER
    public abstract class ServiceHubContext<T> : IHubContext<Hub<T>, T>, IDisposable, IAsyncDisposable where T : class
#else
    public abstract class ServiceHubContext<T> : IHubContext<Hub<T>, T>, IDisposable where T : class
#endif
    {
        public abstract IHubClients<T> Clients { get; }

        public abstract GroupManager Groups { get; }

        public abstract UserGroupManager UserGroups { get; }

        public abstract ClientManager ClientManager { get; }

        IGroupManager IHubContext<Hub<T>, T>.Groups => Groups;

        /// <summary>
        /// Performs a negotiation operation asynchronously that routes a client to a Azure SignalR instance.
        /// </summary>
        /// <returns>A negotiation response object that contains an endpoint url and an access token for the client to connect to the Azure SignalR instance. </returns>
        public abstract ValueTask<NegotiationResponse> NegotiateAsync(NegotiationOptions negotiationOptions = null, CancellationToken cancellationToken = default);

        public abstract ValueTask DisposeAsync();

        public abstract void Dispose();
    }
}