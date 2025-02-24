// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.E2ETests.Common;

internal sealed class IngressHeaderProvider : ICustomHeaderProvider
{
    public const string AsrsInternalForwardedBy = "X-ASRS-Internal-Forwarded-By";

    public const string PodA = "podA";

    public const string PodB = "podB";

    private static readonly string[] Ingresses = [PodA, PodB];

    private static readonly Random Random = new();

    public IEnumerable<(string key, string val)> Headers
    {
        get
        {
            var podName = Ingresses[Random.Next() % Ingresses.Length];
            yield return (AsrsInternalForwardedBy, podName);
        }
    }
}
