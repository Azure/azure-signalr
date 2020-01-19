// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal abstract class ServiceScaleManagerBase : IServiceScaleManager
    {
        private readonly IServiceEndpointManager _serviceEndpointManager;
        private readonly IMultiEndpointServiceContainerManager _multiEndpointManager;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        public ServiceScaleManagerBase(IServiceEndpointManager serviceEndpointManager,
            IMultiEndpointServiceContainerManager multiEndpointManager,
            ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)); ;
            _logger = loggerFactory?.CreateLogger<ServiceScaleManagerBase>();
            _serviceEndpointManager = serviceEndpointManager;
            _multiEndpointManager = multiEndpointManager;
        }

        public virtual Task AddServiceEndpoint(ServiceEndpoint endpoint)
        {
            // 1. per hub get MultiSEContainer
            // 2. MultiSEContainer.AddServiceEndpoint()
            // 2.1  Create inner container and start server connections
            // 2.2. Call MultiSEContainer.StartGetServersPing() to trigger service ping and get global server ids
            // 2.3. Wait MultiSEContainer.IsStable() to check server connection setup successfully
            // 2.4. MultiSEContainer.StopGetServersPing() to stop ping
            // 3. ServiceEndpointManager.AddServiceEndpointToNegotiation() to enable negotiation in server side.
            throw new NotImplementedException();
        }

        public virtual Task RemoveServiceEndpoint(ServiceEndpoint endpoint)
        {
            // 1. per hub get MultiSEContainer
            // 2. ServiceEndpointManager.RemoveServiceEndpointFromNegotiation()
            // 3. MultiSEContainer.RemoveServiceEndpoint()
            // 3.1 OfflineAsync() to trigger `fin` ping 
            // 3.2 Wait MultiSEContainer.IsEndpointActive(endpoint) to check clients for the endpoint are diconnected
            // 3.3 MultiSEContainer.TryRemoveServiceEndpoint() to stop server connections and remove local container
            throw new NotImplementedException();
        }

        public IEnumerable<ServiceEndpoint> GetServiceEndpoints(string hub)
        {
            return _serviceEndpointManager.GetEndpoints(hub);
        }
    }
}
