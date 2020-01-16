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
        private static readonly TimeSpan DefaultScaleTimeout = TimeSpan.FromMinutes(15);

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
            // 2. MultiSEContainer.TryAddServiceEndpoint()
            // 3. When all success, call MultiSEContainer.StartGetServersPing() to trigger service ping and get global server ids
            // 4. Wait MultiSEContainer.IsStable() to check server connection setup successfully
            // 5. MultiSEContainer.StopGetServersPing() to stop ping
            // 6. ServiceEndpointManager.AddServiceEndpointToNegotiation() to enable negotiation in server side.

            // var hubs = _multiEndpointServiceContainerFactory.GetHubs();
            // var isCreated = new List<bool>();
            // foreach (var hub in hubs)
            // {
            //     _multiEndpointManager.TryGet(hub, out var container);
            //     var hubEndpoint = _serviceEndpointManager.GenerateHubServiceEndpoint(hub, endpoint);
            //     if (await container.TryAddServiceEndpoint(hubEndpoint))
            //     {
            //         // container.StartGetServersPing()
            //         // _ = WaitForConnectionStart(container);
            //     }
            // }

            throw new NotImplementedException();
        }

        public virtual Task RemoveServiceEndpoint(ServiceEndpoint endpoint)
        {
            // 1. ServiceEndpointManager.RemoveServiceEndpointFromNegotiation()
            // 2. MultiSEContainer.StartRemoveServiceEndpoint() to trigger `Fin` ping.
            // 3. Wait MultiSEContainer.IsEndpointActive(endpoint) to check clients for the endpoint are diconnected
            // 4. MultiSEContainer.TryRemoveServiceEndpoint() to stop server connections and remove local container
            throw new NotImplementedException();
        }

        public IEnumerable<ServiceEndpoint> GetServiceEndpoints(string hub)
        {
            return _serviceEndpointManager.GetEndpoints(hub);
        }

        private async Task WaitForConnectionStart(IMultiEndpointServiceConnectionContainer container, string hub, ServiceEndpoint endpoint)
        {
            var startWait = DateTime.UtcNow;
            while (DateTime.UtcNow - startWait < DefaultScaleTimeout)
            {
                // if succeed
                // if (container.IsStable())
                // {
                //      container.StopGetServersPing();
                //      _serviceEndpointManager.AddServiceEndpointToNegotiation(hub, endpoint);
                // }
                // wait 3s for next try
                await Task.Delay(3000);
            }
            Log.TimeoutAddingNewEndpoint(_logger, endpoint.Endpoint, endpoint.ToString(), DefaultScaleTimeout.Minutes);
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, string, int, Exception> _timeoutAddingNewEndpoint =
                LoggerMessage.Define<string, string, int>(LogLevel.Error, new EventId(1, "TimeoutAddingNewEndpoint"), "Timeout adding new endpoint '{endpoint}', name '{name}' in {timeoutMinute} minutes. Check if app configurations are consistant and restart app server.");

            public static void TimeoutAddingNewEndpoint(ILogger logger, string endpoint, string name, int timeoutMinute)
            {
                _timeoutAddingNewEndpoint(logger, endpoint, name, timeoutMinute, null);
            }
        }
    }
}
