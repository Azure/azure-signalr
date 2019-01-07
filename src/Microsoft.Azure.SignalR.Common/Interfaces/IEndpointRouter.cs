// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// TODO: expose
    /// </summary>
    internal interface IEndpointRouter
    {
        /// <summary>
        /// TODO: add HttpContext for Core and HostContext for AspNet one?
        /// </summary>
        /// <param name="primaryEndpoints"></param>
        /// <returns></returns>
        ServiceEndpoint GetNegotiateEndpoint(IReadOnlyList<ServiceEndpoint> primaryEndpoints);
    }
}
