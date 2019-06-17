// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    internal class StatusChange
    {
        public StatusChange(ServiceConnectionStatus oldStatus, ServiceConnectionStatus newStatus)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }

        public ServiceConnectionStatus OldStatus { get; }
        public ServiceConnectionStatus NewStatus { get; }
    }
}
