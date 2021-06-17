// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    internal interface IServiceHubLifetimeManager : IHubLifetimeManager, IUserGroupHubLifetimeManager
    {
        Task CloseConnectionAsync(string connectionId, string reason, CancellationToken cancellationToken);

        Task<bool> ConnectionExistsAsync(string connectionId, CancellationToken cancellationToken);

        Task<bool> UserExistsAsync(string userId, CancellationToken cancellationToken);

        Task<bool> GroupExistsAsync(string groupName, CancellationToken cancellationToken);

        Task DisposeAsync();
    }
}