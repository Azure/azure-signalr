// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR.Common.ServiceConnections
{
    internal class ClientConnectionScopeProperties
    {
        public IServiceConnection OutboundServiceConnection { get; set; }
        // todo: extend with client connection tracking/logging settings
    }
}
