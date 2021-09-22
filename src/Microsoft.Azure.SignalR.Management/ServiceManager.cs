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

        /// <summary>
        /// Creates an instance of <see cref="ServiceHubContext{T}"/> asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of client.</typeparam>
        /// <param name="hubName">The name of the hub.</param>
        /// <param name="cancellationToken">Used to abort the creation of the hub.</param>
        /// <returns></returns>
        public virtual Task<ServiceHubContext<T>> CreateHubContextAsync<T>(string hubName, CancellationToken cancellationToken) where T : class => throw new NotImplementedException();

        public abstract Task<bool> IsServiceHealthy(CancellationToken cancellationToken);

        public abstract void Dispose();
    }
}