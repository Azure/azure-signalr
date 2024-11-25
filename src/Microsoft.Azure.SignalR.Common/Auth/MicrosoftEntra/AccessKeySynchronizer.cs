// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR;

internal sealed class AccessKeySynchronizer : IAccessKeySynchronizer, IDisposable
{
    private readonly ConcurrentDictionary<MicrosoftEntraAccessKey, bool> _keyMap = new(ReferenceEqualityComparer.Instance);

    private readonly ILogger<AccessKeySynchronizer> _logger;

    private readonly TimerAwaitable _timer = new TimerAwaitable(TimeSpan.Zero, TimeSpan.FromMinutes(1));

    internal IEnumerable<MicrosoftEntraAccessKey> InitializedKeyList => _keyMap.Where(x => x.Key.Initialized).Select(x => x.Key);

    public AccessKeySynchronizer(ILoggerFactory loggerFactory) : this(loggerFactory, true)
    {
    }

    /// <summary>
    /// Test only.
    /// </summary>
    internal AccessKeySynchronizer(ILoggerFactory loggerFactory, bool start)
    {
        if (start)
        {
            _ = UpdateAllAccessKeyAsync();
        }
        _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<AccessKeySynchronizer>();
    }

    public void AddServiceEndpoint(ServiceEndpoint endpoint)
    {
        if (endpoint.AccessKey is MicrosoftEntraAccessKey key)
        {
            _keyMap.TryAdd(key, true);
        }
    }

    public void Dispose() => _timer.Stop();

    public void UpdateServiceEndpoints(IEnumerable<ServiceEndpoint> endpoints)
    {
        _keyMap.Clear();
        foreach (var endpoint in endpoints)
        {
            AddServiceEndpoint(endpoint);
        }
    }

    /// <summary>
    /// Test only
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    internal bool ContainsKey(ServiceEndpoint e) => _keyMap.ContainsKey(e.AccessKey as MicrosoftEntraAccessKey);

    /// <summary>
    /// Test only
    /// </summary>
    /// <returns></returns>
    internal int Count() => _keyMap.Count;

    private async Task UpdateAllAccessKeyAsync()
    {
        using (_timer)
        {
            _timer.Start();

            while (await _timer)
            {
                foreach (var key in InitializedKeyList)
                {
                    var source = new CancellationTokenSource(Constants.Periods.DefaultUpdateAccessKeyTimeout);
                    _ = key.UpdateAccessKeyAsync(source.Token);
                }
            }
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<MicrosoftEntraAccessKey>
    {
        internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        private ReferenceEqualityComparer()
        {
        }

        public bool Equals(MicrosoftEntraAccessKey x, MicrosoftEntraAccessKey y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(MicrosoftEntraAccessKey obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
