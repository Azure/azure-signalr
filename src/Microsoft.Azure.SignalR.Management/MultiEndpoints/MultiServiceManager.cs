// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR.Management.MultiEndpoints;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Management
{
    internal class MultiServiceManager : IMultiServiceManager
    {
        private readonly Dictionary<ServiceEndpoint, IServiceManager> _serviceManagerTable = new Dictionary<ServiceEndpoint, IServiceManager>();
        private readonly IEnumerable<ServiceEndpoint> _endpointsForNegotiateRouting;
        private readonly IEndpointRouter _router;
        private readonly IEnumerable<ServiceStatusMonitor> _statusMonitors;
        private bool _disposedValue;

        internal MultiServiceManager(IEnumerable<IServiceManager> serviceManagers, IEnumerable<ServiceEndpoint> endpoints, IEndpointRouter router)
        {
            if (serviceManagers is null)
            {
                throw new ArgumentNullException(nameof(serviceManagers));
            }

            if (endpoints is null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            _router = router ?? throw new ArgumentNullException(nameof(router));

            _endpointsForNegotiateRouting = new List<ServiceEndpoint>(endpoints.Count());
            _statusMonitors = new List<ServiceStatusMonitor>(endpoints.Count());
            foreach (var pair in endpoints.Zip(serviceManagers, (EndPoint, Manager) => (EndPoint, Manager)))
            {
                _serviceManagerTable[pair.EndPoint] = pair.Manager;
                var endpointForNegotiateRouting = new ServiceEndpoint(pair.EndPoint);
                _endpointsForNegotiateRouting.Append(endpointForNegotiateRouting);
                _statusMonitors.Append(new ServiceStatusMonitor(pair.Manager, endpointForNegotiateRouting));
            }
        }

        public async Task<IServiceHubContext> CreateHubContextAsync(string hubName, ILoggerFactory loggerFactory = null, CancellationToken cancellationToken = default)
        {
            IEnumerable<(ServiceEndpoint Key, Task<IServiceHubContext>)> endpoint_contextCreationTasks =
                _serviceManagerTable.Select(pair => (pair.Key, pair.Value.CreateHubContextAsync(hubName, loggerFactory, cancellationToken)));
            var t = Task.WhenAll(endpoint_contextCreationTasks.Select(pair => pair.Item2));
            try
            {
                await t;
            }
            catch
            {
                throw t.Exception;
            }
            var hubContextTable = endpoint_contextCreationTasks.ToDictionary(pair => pair.Key, pair => pair.Item2.Result);
            IServiceHubContext multiServiceHubContext = new MultiServiceHubContext(_router, hubContextTable);
            return multiServiceHubContext;
        }

        public async Task<bool> IsServiceHealthy(CancellationToken cancellationToken)
        {
            var t = Task.WhenAll(_serviceManagerTable.Values.Select(manager => manager.IsServiceHealthy(cancellationToken)));
            bool[] healthyStatus = null;
            try
            {
                healthyStatus = await t;
            }
            catch
            {
                throw t.Exception;
            }
            return healthyStatus.Aggregate((b1, b2) => b1 && b2);
        }

        public (string, string) GenerateClientEndpointAndAccessTokenPair(HttpContext context, string hubName, string userId, IList<Claim> claims, TimeSpan? lifeTime)
        {
            var serviceEndpoint = _router.GetNegotiateEndpoint(context, _endpointsForNegotiateRouting);
            var clientEndpoint = _serviceManagerTable[serviceEndpoint].GetClientEndpoint(hubName);
            var token = _serviceManagerTable[serviceEndpoint].GenerateClientAccessToken(hubName, userId, claims, lifeTime);
            return (clientEndpoint, token);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    foreach (var monitor in _statusMonitors)
                    {
                        monitor.Dispose();
                    }
                }
                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}