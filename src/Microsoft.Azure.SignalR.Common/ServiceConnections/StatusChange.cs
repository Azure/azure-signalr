// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR;

internal class StatusChange
{
    public ServiceConnectionStatus OldStatus { get; }

    public ServiceConnectionStatus NewStatus { get; }

    public StatusChange(ServiceConnectionStatus oldStatus, ServiceConnectionStatus newStatus)
    {
        OldStatus = oldStatus;
        NewStatus = newStatus;
    }
}
