// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR;

/// <summary>
/// Manager for presence operations.
/// </summary>
internal interface IPresenceManager
{
    IAsyncEnumerable<GroupMember> ListConnectionsInGroupAsync(string groupName, int? top = null, ulong? tracingId = null, CancellationToken token = default);
}
