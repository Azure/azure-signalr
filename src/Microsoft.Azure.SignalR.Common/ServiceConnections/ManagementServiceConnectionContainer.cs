// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.Common.ServiceConnections
{
    internal class ManagementServiceConnectionContainer : WeakServiceConnectionContainer
    {
        private bool _manuallyStop;

        public ManagementServiceConnectionContainer(IServiceConnectionFactory serviceConnectionFactory,
            int fixedConnectionCount, HubServiceEndpoint endpoint, ILogger logger = null) : base(
            serviceConnectionFactory, fixedConnectionCount, endpoint, logger)
        {
            _manuallyStop = false;
        }

        protected override async Task StartCoreAsync(IServiceConnection connection, string target = null)
        {
            try
            {
                await connection.StartAsync(target);
            }
            finally
            {
                if (!_manuallyStop)
                {
                    await OnConnectionComplete(connection);
                }
            }
        }

        public override Task StopAsync()
        {
            _manuallyStop = true;
            return Task.WhenAll(FixedServiceConnections.Select(c => c.StopAsync()));
        }

        // for test only
        public ServiceConnectionStatus GetServiceConnectionStatus() => GetStatus();
    }
}
