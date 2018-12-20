// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalRService
{
    public class ServiceManager : IServiceManager
    {
        public Task<IServiceHubContext> CreateHubContextAsync(string hubName)
        {
            throw new NotImplementedException();
        }

        public string GenerateClientAccessToken(IList<Claim> claims, TimeSpan? lifeTime = null)
        {
            throw new NotImplementedException();
        }
    }
}
