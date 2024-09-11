// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR;

internal interface IAccessKeySynchronizer
{
    public void AddServiceEndpoint(ServiceEndpoint endpoint);

    public void UpdateServiceEndpoints(IEnumerable<ServiceEndpoint> endpoints);
}
