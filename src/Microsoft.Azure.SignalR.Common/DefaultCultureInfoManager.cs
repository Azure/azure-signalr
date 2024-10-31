// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Azure.SignalR
{
    internal class DefaultCultureInfoManager : ICultureInfoManager
    {
        private static readonly TimeSpan CultureCacheTimeout = TimeSpan.FromSeconds(30);

        private static readonly long CultureCacheTimeoutTicks = (long)(CultureCacheTimeout.TotalSeconds * Stopwatch.Frequency);


        private readonly ConcurrentDictionary<string, RequestCultureWithTimestamp> _cultures = new ConcurrentDictionary<string, RequestCultureWithTimestamp>();

        public bool TryAddCulture(string requestId, CultureInfo culture, CultureInfo uiCulture)
        {
            return _cultures.TryAdd(requestId, new RequestCultureWithTimestamp(culture, uiCulture, Stopwatch.GetTimestamp()));
        }

        public bool TryApplyCulture(string requestId)
        {
            if (_cultures.TryRemove(requestId, out var requestCulture))
            {
                (CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture) = (requestCulture.Culture, requestCulture.UICulture);
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
                    if (Stopwatch.GetTimestamp() - cultureWithTimestamp.Timestamp > CultureCacheTimeoutTicks)
                    {
                        _cultures.TryRemove(key, out _);
                    }
                }
            }
        }

        private record RequestCultureWithTimestamp(CultureInfo Culture, CultureInfo UICulture, long Timestamp)
        {
        }
    }
}
