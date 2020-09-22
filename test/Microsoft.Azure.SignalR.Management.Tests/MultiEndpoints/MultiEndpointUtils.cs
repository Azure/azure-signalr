// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

namespace Microsoft.Azure.SignalR.Management.Tests.MultiEndpoints
{
    public class MultiEndpointUtils
    {
        public static ServiceEndpoint[] GenerateServiceEndpoints(int count)
        {
            return Enumerable.Range(0, count)
.Select(id => new ServiceEndpoint($"Endpoint=http://endpoint{id};AccessKey=accessKey;Version=1.0;"))
.ToArray();
        }
    }
}
