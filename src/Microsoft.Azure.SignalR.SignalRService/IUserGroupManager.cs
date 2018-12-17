// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.SignalRService
{
    public interface IUserGroupManager
    {
        Task AddToGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default);
        Task RemoveFromGroupAsync(string userId, string groupName, CancellationToken cancellationToken = default);
    }
}