// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IClientConnectionFactory
    {
        ServiceConnectionContext CreateConnection(OpenConnectionMessage message);
    }
}
