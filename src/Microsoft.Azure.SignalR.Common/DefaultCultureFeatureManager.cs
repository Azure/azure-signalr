// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

# if NETSTANDARD2_0 || NET6_0_OR_GREATER
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.AspNetCore.Localization;

namespace Microsoft.Azure.SignalR;

internal class DefaultCultureFeatureManager : ICultureFeatureManager
{
    private readonly long _cacheTimeoutTicks;

    private readonly ConcurrentDictionary<string, RequestCultureFeatureWithTimestamp> _cultures = new ConcurrentDictionary<string, RequestCultureFeatureWithTimestamp>();

    public DefaultCultureFeatureManager(long cacheTimeoutInSecond = 30)
    {
        _cacheTimeoutTicks = cacheTimeoutInSecond * Stopwatch.Frequency;
    }

    public bool TryAddCultureFeature(string requestId, IRequestCultureFeature feature)
    {
        return _cultures.TryAdd(requestId, new RequestCultureFeatureWithTimestamp(feature, Stopwatch.GetTimestamp()));
    }

    public bool TryRemoveCultureFeature(string requestId, out IRequestCultureFeature feature)
    {
        if (_cultures.TryRemove(requestId, out var featureWithTimeout))
        {
            feature = featureWithTimeout.Feature;
            return true;
        }
        feature = null;
        return false;
    }

    public void Cleanup()
    {
        foreach (var item in _cultures)
        {
            if (_cultures.TryGetValue(item.Key, out var cultureWithTimestamp))
            {
                if (Stopwatch.GetTimestamp() - cultureWithTimestamp.Timestamp > _cacheTimeoutTicks)
                {
                    _cultures.TryRemove(item.Key, out _);
                }
            }
        }
    }

    private record RequestCultureFeatureWithTimestamp(IRequestCultureFeature Feature, long Timestamp)
    {
    }
}
#endif
