// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Azure.SignalR.Management
{
    internal sealed class ServiceHubContextImpl<T> : ServiceHubContext<T> where T : class
    {
        private readonly string _hubName;
        private readonly NegotiateProcessor _negotiateProcessor;
        private readonly IServiceHubLifetimeManager _lifetimeManager;
        private readonly IServiceEndpointManager _endpointManager;
        private bool _disposing;

        internal IServiceProvider ServiceProvider { get; }

        public override IHubClients<T> Clients { get; }

        public override GroupManager Groups { get; }

        public override UserGroupManager UserGroups { get; }

        public override ClientManager ClientManager { get; }

        public ServiceHubContextImpl(string hubName, IHubContext<Hub<T>, T> typedHubContext, NegotiateProcessor negotiateProcessor, IServiceHubLifetimeManager lifetimeManager, IServiceEndpointManager endpointManager, IServiceProvider serviceProvider)
        {
            _hubName = hubName;
            _negotiateProcessor = negotiateProcessor;
            _lifetimeManager = lifetimeManager;
            _endpointManager = endpointManager;
            ServiceProvider = serviceProvider;
            Clients = typedHubContext.Clients;
            Groups = new GroupManagerAdapter(lifetimeManager);
            UserGroups = new UserGroupsManagerAdapter(lifetimeManager);
            ClientManager = new ClientManagerAdapter(lifetimeManager);
        }

        public override ValueTask<NegotiationResponse> NegotiateAsync(NegotiationOptions negotiationOptions = null, CancellationToken cancellationToken = default) => new(_negotiateProcessor.NegotiateAsync(_hubName, negotiationOptions, cancellationToken));

        public override Task<NegotiationResult> NegotiateWithTokenLifetimeAsync(NegotiationOptions negotiationOptions = null, CancellationToken cancellationToken = default) => _negotiateProcessor.NegotiateWithTokenLifetimeAsync(_hubName, negotiationOptions, cancellationToken);

        public override Task<RefreshConnectionAuthenticationResult> RefreshConnectionAuthenticationAsync(string connectionToken, DateTimeOffset expireTime, IEnumerable<Claim> claims = null, CancellationToken cancellationToken = default) =>
            ConnectionAuthenticationHelper.RefreshConnectionAuthenticationAsync(_hubName, _lifetimeManager, _endpointManager, connectionToken, expireTime, claims, cancellationToken);

        public override Task<ConnectionClaims> GetConnectionClaimsAsync(string connectionToken, CancellationToken cancellationToken = default) =>
            ConnectionAuthenticationHelper.GetConnectionClaimsAsync(_lifetimeManager, connectionToken, cancellationToken);

        public override async ValueTask DisposeAsync()
        {
            // check _disposed to avoid being dispose twice.
            // when _baseHubContext dispose, it will dispose all the disposable services including this class.
            await DisposeAsyncCore();
        }

        public override void Dispose()
        {
            if (!_disposing)
            {
                DisposeAsyncCore().GetAwaiter().GetResult();
            }
        }

        private async Task DisposeAsyncCore()
        {
            if (!_disposing)
            {
                _disposing = true;
                using var host = ServiceProvider.GetRequiredService<IHost>();
                await host.StopAsync();
            }
        }
    }
}
