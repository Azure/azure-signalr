// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubLifetimeManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ContextOptions _context;

        public ServiceHubLifetimeManagerFactory(IServiceProvider sp, IOptions<ContextOptions> context)
        {
            _serviceProvider = sp;
            _context = context.Value;
        }

        public  IServiceHubLifetimeManager Create(string hubName)
        {
            switch (_context.ServiceTransportType)
            {
                case ServiceTransportType.Persistent:
                    {
                        var container = _serviceProvider.GetRequiredService<IServiceConnectionContainer>();
                        var connectionManager = new ServiceConnectionManager<Hub>();
                        connectionManager.SetServiceConnection(container);
                        return ActivatorUtilities.CreateInstance<WebSocketsHubLifetimeManager<Hub>>(_serviceProvider, connectionManager);
                    }
                case ServiceTransportType.Transient:
                    {
                        return new RestHubLifetimeManager(hubName, _context.ServiceEndpoints.Single(), _context.ProductInfo, _context.ApplicationName);
                    }
                default: throw new InvalidEnumArgumentException(nameof(ContextOptions.ServiceTransportType), (int)_context.ServiceTransportType, typeof(ServiceTransportType));
            }
        }
    }
}