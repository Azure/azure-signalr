// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Sets up <see cref="ServiceManagerContext"/> from <see cref="ServiceManagerOptions"/> and allows tracking changes.
    /// </summary>
    internal class ServiceManagerContextSetup : IConfigureOptions<ServiceManagerContext>, IOptionsChangeTokenSource<ServiceManagerContext>
    {
        private readonly IOptionsMonitor<ServiceManagerOptions> _monitor;
        private readonly IOptionsChangeTokenSource<ServiceManagerOptions> _tokenSource;

        public ServiceManagerContextSetup(IOptionsMonitor<ServiceManagerOptions> monitor, IOptionsChangeTokenSource<ServiceManagerOptions> tokenSource = null)//Making 'tokenSource' optional avoids error when ServiceManagerOptions is configured with delegate and 'tokenSource' is unavailable.
        {
            _monitor = monitor;
            _tokenSource = tokenSource;
        }

        public string Name => Options.DefaultName;

        public void Configure(ServiceManagerContext options)
        {
            var serviceManagerOptions = _monitor.CurrentValue;
            options.ServiceEndpoints = serviceManagerOptions.ServiceEndpoints ?? (serviceManagerOptions.ServiceEndpoint != null
                    ? (new ServiceEndpoint[] { serviceManagerOptions.ServiceEndpoint })
                    : (new ServiceEndpoint[] { new ServiceEndpoint(serviceManagerOptions.ConnectionString) }));
            options.ApplicationName = serviceManagerOptions.ApplicationName;
            options.ConnectionCount = serviceManagerOptions.ConnectionCount;
            options.Proxy = serviceManagerOptions.Proxy;
            options.ServiceTransportType = serviceManagerOptions.ServiceTransportType;
        }

        public IChangeToken GetChangeToken()
        {
            return _tokenSource.GetChangeToken();
        }
    }
}