// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    public interface IServiceScaleManager
    {
        Task AddServiceEndpoint(ServiceEndpoint endpoint);

        Task RemoveServiceEndpoint(ServiceEndpoint endpoint);

        IEnumerable<ServiceEndpoint> GetServiceEndpoints(string hub);
    }
}
