// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    public interface IServiceContext : IDisposable
    {
        /// <summary>
        /// Creates an instance of <see cref="IServiceHubContext"/> asynchronously.
        /// </summary>
        /// <param name="hubName">The hub name.</param>
        /// <param name="cancellationToken">Cancellation token for creating service hub context.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        Task<IServiceHubContext> CreateHubContextAsync(string hubName, CancellationToken cancellationToken = default);
    }
}