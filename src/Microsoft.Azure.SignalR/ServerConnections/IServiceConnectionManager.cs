// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR;

internal interface IServiceConnectionManager<THub> : IServiceConnectionManager where THub : Hub
{
    void SetServiceConnection(IServiceConnectionContainer serviceConnection);
}
