// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

# if NETSTANDARD2_0 || NET6_0_OR_GREATER
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Microsoft.Azure.SignalR;

internal class DefaultCultureInfoManager : ICultureInfoManager
{
    private readonly long _cacheTimeoutTicks;

    private readonly ConcurrentDictionary<string, RequestCultureFeatureWithTimestamp> _cultures = new ConcurrentDictionary<string, RequestCultureFeatureWithTimestamp>();

    public DefaultCultureInfoManager(long cacheTimeoutInSecond = 30)
    {
        _cacheTimeoutTicks = cacheTimeoutInSecond * Stopwatch.Frequency;
    }

    public bool TryAddCulture(string requestId, IRequestCultureFeature feature)
    {
        return _cultures.TryAdd(requestId, new RequestCultureFeatureWithTimestamp(feature, Stopwatch.GetTimestamp()));
    }

    public bool TryApplyCulture(string requestId)
    {
        if (_cultures.TryRemove(requestId, out var featureWithTimeout))
        {
            (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (featureWithTimeout.Feature.RequestCulture.Culture, featureWithTimeout.Feature.RequestCulture.UICulture);
            return true;
        }
        return false;
    }

    public void Cleanup()
    {
        foreach (var key in _cultures.Keys)
        {
            if (_cultures.TryGetValue(key, out var cultureWithTimestamp))
            {
                if (Stopwatch.GetTimestamp() - cultureWithTimestamp.Timestamp > _cacheTimeoutTicks)
                {
                    _cultures.TryRemove(key, out _);
                }
            }
        }
    }

    private record RequestCultureFeatureWithTimestamp(IRequestCultureFeature Feature, long Timestamp)
    {
    }
}
#endif