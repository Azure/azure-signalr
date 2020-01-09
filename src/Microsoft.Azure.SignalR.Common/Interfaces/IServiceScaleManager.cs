// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR
{
    internal interface IServiceScaleManager
    {
        void AddMultipleEndpointServiceConnectionContainer(IMultiEndpointServiceConnectionContainer container);

        IMultiEndpointServiceConnectionContainer GetMultipleEndpointServiceConnectionContainer(string hub);

        IReadOnlyList<string> GetHubs();

        Task AddServiceEndpoint(ServiceEndpoint endpoint);

        Task RemoveServiceEndpoint(ServiceEndpoint endpoint);
    }
}
