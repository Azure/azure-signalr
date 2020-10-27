// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Sets up <see cref="ServiceOptions"/> from <see cref="ServiceManagerContext"/> and tracks changes.
    /// </summary>
    internal class ServiceOptionsSetup : IConfigureOptions<ServiceOptions>, IOptionsChangeTokenSource<ServiceOptions>
    {
        private readonly IOptionsMonitor<ServiceManagerContext> _monitor;
        private readonly IOptionsChangeTokenSource<ServiceManagerContext> _changeTokenSource;

        public ServiceOptionsSetup(IOptionsMonitor<ServiceManagerContext> monitor, IOptionsChangeTokenSource<ServiceManagerContext> changeTokenSource = null)//Making 'tokenSource' optional avoids error when 'tokenSource' is unavailable.
        {
            _monitor = monitor;
            _changeTokenSource = changeTokenSource;
        }

        public string Name => Options.DefaultName;

        public void Configure(ServiceOptions options)
        {
            var context = _monitor.CurrentValue;
            options.ApplicationName = context.ApplicationName;
            options.Endpoints = context.ServiceEndpoints;
            options.Proxy = context.Proxy;
            options.ConnectionCount = context.ConnectionCount;
        }

        public IChangeToken GetChangeToken()
        {
            return _changeTokenSource.GetChangeToken();
        }
    }
}