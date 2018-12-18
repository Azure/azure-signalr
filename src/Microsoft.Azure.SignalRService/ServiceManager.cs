// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalRService
{
    public class ServiceManager
    {
        public Task<IServiceHubContext> CreateHubContextAsync(string hubName)
        {
            throw new NotImplementedException();
        }

        public string GenerateAccessToken(Scope scope, string hubName, string connectionId = null, string userId = null, string groupName = null, TimeSpan? lifeTime = null)
        {
            throw new NotImplementedException();
        }
    }
}
