﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        public ServiceHubContextFactory(ServiceHubLifetimeManagerFactory managerFactory)
        {
            _managerFactory = managerFactory;
        }

        public async Task<IServiceHubContext> CreateAsync(string hubName, ILoggerFactory loggerFactory = null, CancellationToken cancellationToken = default)
        {
            var manager = await _managerFactory.CreateAsync(hubName, cancellationToken, loggerFactory);
            var servicesPerHub = new ServiceCollection();
            servicesPerHub.AddSignalRCore();
            servicesPerHub.AddSingleton((HubLifetimeManager<Hub>)manager);
            var serviceProviderPerHub = servicesPerHub.BuildServiceProvider();
            // The impl of IHubContext<Hub> we want is an internal class. We can only get it by this way.
            var hubContext = serviceProviderPerHub.GetRequiredService<IHubContext<Hub>>();
            return new ServiceHubContext(hubContext, manager, serviceProviderPerHub);
        }
    }
}