// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    public abstract class ServiceManager : IDisposable
    {
        public abstract Task<ServiceHubContext> CreateHubContextAsync(string hubName, CancellationToken cancellationToken);

        internal virtual Task<ServiceHubContext<T>> CreateHubContextAsync<T>(string hubName, CancellationToken cancellationToken) where T : class => throw new NotImplementedException();

        public abstract Task<bool> IsServiceHealthy(CancellationToken cancellationToken);

        public abstract void Dispose();
    }
}