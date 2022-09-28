// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    internal class ServiceUserIdFeature : IServiceUserIdFeature
    {
        public string UserId { get; }

        public ServiceUserIdFeature(string userId)
        {
            UserId = userId;
        }
    }
}
