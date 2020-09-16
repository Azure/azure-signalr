// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class MultiServiceHubContext : IServiceHubContext
    {
        private readonly IEndpointRouter _router;
        private readonly Dictionary<ServiceEndpoint, IServiceHubContext> _hubContextTable;

        internal MultiServiceHubContext(IEndpointRouter router, Dictionary<ServiceEndpoint, IServiceHubContext> hubContextTable)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _hubContextTable = hubContextTable ?? throw new ArgumentNullException(nameof(hubContextTable));
            UserGroups = new MultiEndpointUserGroupManager(_router, _hubContextTable.ToDictionary(pair => pair.Key, pair => pair.Value.UserGroups));
            Clients = new MultiEndpointHubClients(_router, _hubContextTable.ToDictionary(pair => pair.Key, pair => pair.Value.Clients));
            Groups = new MultiEndpointGroupManager(_router, _hubContextTable.ToDictionary(pair => pair.Key, pair => pair.Value.Groups));
        }

        public IUserGroupManager UserGroups { get; }

        public IHubClients Clients { get; }

        public IGroupManager Groups { get; }

        public Task DisposeAsync()
        {
            return Task.WhenAll(_hubContextTable.Values.Select(context => context.DisposeAsync()));
        }
    }
}