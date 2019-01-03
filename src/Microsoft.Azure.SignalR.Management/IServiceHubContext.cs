// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.AspNetCore.SignalR;

namespace Microsoft.Azure.SignalR.Management
{
    public interface IServiceHubContext: IDisposable, IHubContext<Hub>
    {
        IUserGroupManager UserGroups { get; }
    }
}
