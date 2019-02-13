// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management
{
    public interface IServiceManager
    {
        Task<IServiceHubContext> CreateHubContextAsync(string hubName);

        string GenerateClientAccessToken(string hubName, string userId, IList<Claim> claims, TimeSpan? lifeTime = null);

        string GetClientEndpoint(string hubName);
    }
}