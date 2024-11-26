// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR;

internal static class ServiceEndpointUtility
{
    public static IEnumerable<ServiceEndpoint> Merge(string connectionString, IEnumerable<ServiceEndpoint> endpoints)
    {
        if (!string.IsNullOrEmpty(connectionString))
        {
            yield return new ServiceEndpoint(connectionString);
        }

        if (endpoints != null)
        {
            foreach (var endpoint in endpoints)
            {
                yield return endpoint;
            }
        }
    }
}