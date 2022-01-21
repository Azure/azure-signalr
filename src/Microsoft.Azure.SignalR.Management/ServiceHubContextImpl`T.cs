// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Management
{
    internal sealed class ServiceHubContextImpl<T> : ServiceHubContext<T> where T : class
    {
        private readonly string _hubName;
        private readonly NegotiateProcessor _negotiateProcessor;
        private bool _disposing;

        internal IServiceProvider ServiceProvider { get; }

        internal ServiceHubContext NonGenericHubContext { get; private set; }

        public static implicit operator ServiceHubContext(ServiceHubContextImpl<T> c) => c.NonGenericHubContext;

        public override IHubClients<T> Clients { get; }

        public override GroupManager Groups { get; }

        public override UserGroupManager UserGroups { get; }

        public override ClientManager ClientManager { get; }

        public ServiceHubContextImpl(string hubName, IHubContext<Hub<T>, T> typedHubContext, NegotiateProcessor negotiateProcessor, IServiceHubLifetimeManager lifetimeManager, IServiceProvider serviceProvider)
        {
            _hubName = hubName;
            _negotiateProcessor = negotiateProcessor;
            ServiceProvider = serviceProvider;
            Clients = typedHubContext.Clients;
            Groups = new GroupManagerAdapter(typedHubContext.Groups);
            UserGroups = new UserGroupsManagerAdapter(lifetimeManager);
            ClientManager = new ClientManagerAdapter(lifetimeManager);
            NonGenericHubContext = ActivatorUtilities.CreateInstance<ServiceHubContextImpl>(ServiceProvider, _hubName);
        }

        public override ValueTask<NegotiationResponse> NegotiateAsync(NegotiationOptions negotiationOptions = null, CancellationToken cancellationToken = default) => new(_negotiateProcessor.NegotiateAsync(_hubName, negotiationOptions, cancellationToken));

        public override async ValueTask DisposeAsync()
        {
            // check _disposed to avoid being dispose twice.
            if (!_disposing)
            {
                _disposing = true;
                await NonGenericHubContext.DisposeAsync();
            }
        }

        public override void Dispose()
        {
            if (!_disposing)
            {
                DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }
    }
}