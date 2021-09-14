// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace Microsoft.Azure.SignalR
{
    internal class DefaultBlazorDetector : IBlazorDetector
    {
        private readonly ConcurrentDictionary<string, bool> _blazor = new ConcurrentDictionary<string, bool>();

        public bool IsBlazor(string hubName)
        {
            _blazor.TryGetValue(hubName, out var isBlazor);
            return isBlazor;
        }

        public bool TrySetBlazor(string hubName, bool isBlazor)
        {
            return _blazor.TryAdd(hubName, isBlazor);
        }
    }
}
