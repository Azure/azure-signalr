// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.SignalR
{
    public enum EndpointRoutingMode
    {
        /// <summary>
        ///  Choose endpoint randomly by weight. 
        ///  The weight is defined as (the remaining connection quota / the connection capacity).
        ///  This is the default mode.
        /// </summary>
        Weighted,
        
        /// <summary>
        /// Choose the endpoint with least connection count.
        /// This mode distributes connections evenly among endpoints.
        /// </summary>
        LeastConnection,
        
        /// <summary>
        /// Choose the endpoint randomly
        /// </summary>
        Random,
    }
}