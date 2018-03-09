// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.AspNetCore.SignalR.Internal.Protocol
{
    public static class HubInvocationMessageExtensions
    {
        public static HubInvocationMessage AddHeaders(this HubInvocationMessage hubMessage, IDictionary<string, string> headers)
        {
            if (hubMessage == null || headers == null || headers.Count == 0) return hubMessage;
            foreach (var kvp in headers)
            {
                hubMessage.Headers.Add(kvp.Key, kvp.Value);
            }
            return hubMessage;
        }
    }
}
