// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal interface IMultiEndpointServiceContainerManager
    {
        void SaveMultipleEndpointServiceConnectionContainer(string hub, IMultiEndpointServiceConnectionContainer container);

        bool TryGet(string hub, out IMultiEndpointServiceConnectionContainer container);

        IReadOnlyList<string> Hubs { get; }
    }
}
