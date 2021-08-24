// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A builder abstraction for configuring <see cref="IServiceManager"/> instances.
    /// </summary>
    [Obsolete]
    public interface IServiceManagerBuilder
    {
        /// <summary>
        /// Builds <see cref="IServiceManager"/> instances.
        /// </summary>
        /// <returns>The instance of the <see cref="IServiceManager"/>.</returns>
        [Obsolete("Use ServiceManagerBuilder.BuildServiceManager() instead.")]
        IServiceManager Build();
    }
}
