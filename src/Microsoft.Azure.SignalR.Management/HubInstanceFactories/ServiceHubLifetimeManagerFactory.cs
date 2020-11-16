﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class ServiceHubLifetimeManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MultiEndpointConnectionContainerFactory _connectionContainerFactory;
        private readonly ServiceManagerContext _context;

        public ServiceHubLifetimeManagerFactory(IServiceProvider sp, IOptions<ServiceManagerContext> context, MultiEndpointConnectionContainerFactory connectionContainerFactory)
        {
            _serviceProvider = sp;
            _connectionContainerFactory = connectionContainerFactory;
            _context = context.Value;
        }

        public async Task<IServiceHubLifetimeManager> CreateAsync(string hubName, CancellationToken cancellationToken, ILoggerFactory loggerFactoryPerHub = null)
        {
            switch (_context.ServiceTransportType)
            {
                case ServiceTransportType.Persistent:
                    {
                        var container = _connectionContainerFactory.Create(hubName, loggerFactoryPerHub);
                        var connectionManager = new ServiceConnectionManager<Hub>();
                        connectionManager.SetServiceConnection(container);
                        _ = connectionManager.StartAsync();
                        await container.ConnectionInitializedTask.OrTimeout(cancellationToken);
                        return loggerFactoryPerHub == null ? ActivatorUtilities.CreateInstance<WebSocketsHubLifetimeManager<Hub>>(_serviceProvider, connectionManager) : ActivatorUtilities.CreateInstance<WebSocketsHubLifetimeManager<Hub>>(_serviceProvider, connectionManager, loggerFactoryPerHub);
                    }
                case ServiceTransportType.Transient:
                    {
                        return new RestHubLifetimeManager(hubName, _context.ServiceEndpoints.Single(), _context.ProductInfo, _context.ApplicationName);
                    }
                default: throw new InvalidEnumArgumentException(nameof(ServiceManagerContext.ServiceTransportType), (int)_context.ServiceTransportType, typeof(ServiceTransportType));
            }
        }
    }
}