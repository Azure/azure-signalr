﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal sealed class ServiceHubContextImpl : ServiceHubContext, IInternalServiceHubContext
    {
        private readonly string _hubName;
        private readonly IHubContext<Hub> _hubContext;
        private readonly NegotiateProcessor _negotiateProcessor;
        private readonly IServiceEndpointManager _endpointManager;
        
        private bool _disposing;
        internal IServiceProvider ServiceProvider { get; }

        public override IHubClients Clients => _hubContext.Clients;

        public override GroupManager Groups { get; }

        public override UserGroupManager UserGroups { get; }

        public override ClientManager ClientManager { get; }

        public ServiceHubContextImpl(string hubName, IHubContext<Hub> hubContext, IServiceHubLifetimeManager lifetimeManager, IServiceProvider serviceProvider, NegotiateProcessor negotiateProcessor, IServiceEndpointManager endpointManager)
        {
            _hubName = hubName;
            _hubContext = hubContext;
            Groups = new GroupManagerAdapter(_hubContext.Groups);
            UserGroups = new UserGroupsManagerAdapter(lifetimeManager);
            ClientManager = new ClientManagerAdapter(lifetimeManager);
            ServiceProvider = serviceProvider;
            _negotiateProcessor = negotiateProcessor;
            _endpointManager = endpointManager;
        }

        public override ValueTask<NegotiationResponse> NegotiateAsync(NegotiationOptions options, CancellationToken cancellationToken)
        {
            return new ValueTask<NegotiationResponse>(_negotiateProcessor.NegotiateAsync(_hubName, options, cancellationToken));
        }

        IEnumerable<ServiceEndpoint> IInternalServiceHubContext.GetServiceEndpoints() => _endpointManager.GetEndpoints(_hubName);

        public override async Task DisposeAsync()
        {
            // Check _disposed to avoid disposing twice.
            // When host is diposed, it will dispose all the disposable services including this class.
            if (!_disposing)
            {
                _disposing = true;
                using var host = ServiceProvider.GetRequiredService<IHost>();
                await host.StopAsync();
            }
        }

        ServiceHubContext IInternalServiceHubContext.WithEndpoints(IEnumerable<ServiceEndpoint> endpoints)
        {
            if (endpoints is null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            var targetEndpoints = _endpointManager.GetEndpoints(_hubName).Intersect(endpoints, EqualityComparer<ServiceEndpoint>.Default).Select(e => e as HubServiceEndpoint).ToList();
            var container = new MultiEndpointMessageWriter(targetEndpoints, ServiceProvider.GetRequiredService<ILoggerFactory>());
            var servicesFromServiceManager = ServiceProvider.GetRequiredService<IReadOnlyCollection<ServiceDescriptor>>();
            var services = new ServiceCollection()
                .Add(servicesFromServiceManager)
                //Allow chained call serviceHubContext.WithEndpoints(...).WithEndpoints(...)
                .AddSingleton(servicesFromServiceManager)
                //overwrite container
                .AddSingleton<IServiceConnectionContainer>(container)
                //add required service instances
                .AddSingleton(ServiceProvider.GetRequiredService<IOptions<ServiceManagerOptions>>())
                .AddSingleton(_endpointManager)
                .AddSingleton<IEndpointRouter>(new FixedEndpointRouter(targetEndpoints));

            return services.BuildServiceProvider().GetRequiredService<ServiceHubContext>();
        }
    }
}