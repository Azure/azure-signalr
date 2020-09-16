// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal class MultiEndpointClientProxy : IClientProxy
    {
        private readonly IEnumerable<IClientProxy> proxies;

        internal MultiEndpointClientProxy(IEnumerable<IClientProxy> proxies)
        {
            this.proxies = proxies ?? throw new System.ArgumentNullException(nameof(proxies));
        }

        public Task SendCoreAsync(string method, object[] args, CancellationToken cancellationToken = default)
        {
            return Task.WhenAll(
                proxies.Select(
                proxy => proxy.SendCoreAsync(method, args, cancellationToken)));
        }
    }
}