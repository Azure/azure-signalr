// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Emulator.HubEmulator
{
    internal interface IDynamicHubContextStore
    {
        bool TryGetLifetimeContext(string hub, out DynamicHubContext context);

        public DynamicHubContext GetOrAdd(string hub);
    }
}