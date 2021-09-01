// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    internal interface IServiceHubLifetimeManager<THub> : IServiceHubLifetimeManager where THub : Hub
    {
    }
}