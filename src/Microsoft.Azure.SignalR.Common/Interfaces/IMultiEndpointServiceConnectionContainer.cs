// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal interface IMultiEndpointServiceConnectionContainer : IServiceConnectionContainer
    {
        public IEnumerable<ServiceEndpoint> GetRoutedEndpoints(ServiceMessage message);
    }
}
