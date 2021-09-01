// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContextImpl<T> : ServiceHubContext<T> where T : class
    {
        private readonly ServiceHubContext _baseHubContext;

        public override IHubClients<T> Clients { get; }

        public override GroupManager Groups => _baseHubContext.Groups;

        public override ClientManager ClientManager => _baseHubContext.ClientManager;

        public override UserGroupManager UserGroupManager => _baseHubContext.UserGroups;

        public ServiceHubContextImpl(ServiceHubContext baseHubContext, IHubContext<Hub<T>,T> typedHubContext)
        {
            _baseHubContext = baseHubContext;
            Clients = typedHubContext.Clients;
        }

        public override Task DisposeAsync() => _baseHubContext.DisposeAsync();

        public override ValueTask<NegotiationResponse> NegotiateAsync(NegotiationOptions negotiationOptions = null, CancellationToken cancellationToken = default) => _baseHubContext.NegotiateAsync(negotiationOptions, cancellationToken);
    }
}
