// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContextFactory
    {
        private readonly ServiceHubLifetimeManagerFactory _managerFactory;
        private readonly IServiceEndpointManager _endpointManager;

        public ServiceHubContextFactory(ServiceHubLifetimeManagerFactory managerFactory, IServiceEndpointManager endpointManager)
        {
            _managerFactory = managerFactory;
            _endpointManager = endpointManager;
        }

        public async Task<IServiceHubContext> CreateAsync(string hubName, ILoggerFactory loggerFactory = null, CancellationToken cancellationToken = default)
        {
            var manager = await _managerFactory.CreateAsync(hubName, cancellationToken, loggerFactory);
            var (hubContext, serviceProviderPerHub) = CreateHubContext(manager);
            return new ServiceHubContext(_endpointManager, hubContext, manager, serviceProviderPerHub, this, hubName);
        }

        public IServiceHubContext Create(string hubName, IEnumerable<ServiceEndpoint> endpoints)
        {
            var manager = _managerFactory.Create(hubName, endpoints);
            var (hubContext, serviceProviderPerHub) = CreateHubContext(manager);
            return new ServiceHubContext(_endpointManager, hubContext, manager, serviceProviderPerHub, this, hubName);
        }

        private (IHubContext<Hub>, ServiceProvider) CreateHubContext(IServiceHubLifetimeManager manager)
        {
            var servicesPerHub = new ServiceCollection();
            servicesPerHub.AddSignalRCore();
            servicesPerHub.AddSingleton((HubLifetimeManager<Hub>)manager);
            var serviceProviderPerHub = servicesPerHub.BuildServiceProvider();
            // The impl of IHubContext<Hub> we want is an internal class. We can only get it by this way.
            return (serviceProviderPerHub.GetRequiredService<IHubContext<Hub>>(), serviceProviderPerHub);
        }
    }
}