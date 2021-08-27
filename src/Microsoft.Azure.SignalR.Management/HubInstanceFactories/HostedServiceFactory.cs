// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR.Management
{
    internal class HostedServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ServiceManagerOptions _options;
        private bool _used = false;

        public HostedServiceFactory(IServiceProvider serviceProvider, IOptions<ServiceManagerOptions> options)
        {
            _serviceProvider = serviceProvider;
            _options = options.Value;
        }

        public IHostedService Create()
        {
            if (_used)
            {
                throw new InvalidOperationException("Don't create multiple IHostedService from this factory.");
            }
            _used = true;
            return _options.ServiceTransportType switch
            {
                ServiceTransportType.Persistent => _serviceProvider.GetRequiredService<ConnectionService>(),
                ServiceTransportType.Transient => _serviceProvider.GetRequiredService<RestHealthCheckService>(),
                _ => throw new NotSupportedException(),
            };
        }
    }
}
