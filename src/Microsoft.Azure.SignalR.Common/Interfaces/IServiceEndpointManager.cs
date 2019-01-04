// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// TODO: support multiple endpoints
    /// </summary>
    internal interface IServiceEndpointManager
    {
        IServiceEndpointProvider GetEndpointProvider();
    }
}
