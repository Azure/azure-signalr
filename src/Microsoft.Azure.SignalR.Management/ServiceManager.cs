﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.SignalR.Management
{
    public class ServiceManager : IServiceManager
    {
        private ServiceManagerOptions _serviceManagerOptions;

        internal ServiceManager(ServiceManagerOptions serviceManagerOptions)
        {
            _serviceManagerOptions = serviceManagerOptions;
        }

        public Task<IServiceHubContext> CreateHubContextAsync(string hubName)
        {
            switch (_serviceManagerOptions.ServiceTransportType)
            {
                case ServiceTransportType.Persistent:
                    {
                        throw new NotImplementedException();
                    }
                case ServiceTransportType.Transient:
                    {
                        var serviceCollection = new ServiceCollection();
                        serviceCollection.AddSignalRCore();

                        // remove default hub lifetime manager
                        var serviceDescriptor = serviceCollection.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(HubLifetimeManager<>));
                        serviceCollection.Remove(serviceDescriptor);

                        // add rest hub lifetime manager
                        var restHubLifetimeManager = new RestHubLifetimeManager(_serviceManagerOptions, hubName);
                        serviceCollection.AddSingleton(typeof(HubLifetimeManager<Hub>), restHubLifetimeManager);

                        var services = serviceCollection.BuildServiceProvider();
                        var hubContext = services.GetRequiredService<IHubContext<Hub>>();
                        var serviceHubContext = new ServiceHubContext(hubContext, restHubLifetimeManager);
                        return Task.FromResult<IServiceHubContext>(serviceHubContext);
                    }
                default:
                    throw new ArgumentException("Not supported service transport type.");
            }
        }

        public string GenerateClientAccessToken(IList<Claim> claims, TimeSpan? lifeTime = null)
        {
            throw new NotImplementedException();
        }
    }
}