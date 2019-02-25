// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// A context abstraction for a hub.
    /// </summary>
    public interface IServiceHubContext : IDisposable, IHubContext<Hub>
    {
        /// <summary>
        /// Gets a user group manager instance which implements <see cref="IUserGroupManager"/> that can be used to add and remove users to named groups.
        /// </summary>
        IUserGroupManager UserGroups { get; }
    }
}