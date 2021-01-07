// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubContextFactory
    {
        private readonly ServiceHubLifetimeManagerFactory _managerFactory;
        private readonly IServiceProvider _serviceProvider;

        public ServiceHubContextFactory(ServiceHubLifetimeManagerFactory managerFactory,IServiceProvider serviceProvider)
        {
            _managerFactory = managerFactory;
            _serviceProvider = serviceProvider;
        }

        public Task<IServiceHubContext> CreateAsync(string hubName)
        {
            var manager = _managerFactory.Create(hubName);
            var servicesPerHub = new ServiceCollection();
            servicesPerHub.AddSignalRCore();
            servicesPerHub.AddSingleton((HubLifetimeManager<Hub>)manager);
            var serviceProviderPerHub = servicesPerHub.BuildServiceProvider();
            // The impl of IHubContext<Hub> we want is an internal class. We can only get it by this way.
            var hubContext = serviceProviderPerHub.GetRequiredService<IHubContext<Hub>>();
            return Task.FromResult(new ServiceHubContext(hubContext, manager, serviceProviderPerHub) as IServiceHubContext);
        }
    }
}