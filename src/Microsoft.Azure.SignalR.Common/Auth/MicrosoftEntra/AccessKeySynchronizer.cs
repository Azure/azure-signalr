// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

internal sealed class AccessKeySynchronizer : IAccessKeySynchronizer, IDisposable
{
    private readonly ConcurrentDictionary<ServiceEndpoint, object> _endpoints = new ConcurrentDictionary<ServiceEndpoint, object>(ReferenceEqualityComparer.Instance);

    private readonly ILoggerFactory _factory;

    private readonly TimerAwaitable _timer = new TimerAwaitable(TimeSpan.Zero, TimeSpan.FromMinutes(1));

    internal IEnumerable<MicrosoftEntraAccessKey> AccessKeyForMicrosoftEntraList => _endpoints.Select(e => e.Key.AccessKey).OfType<MicrosoftEntraAccessKey>();

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
            _ = UpdateAccessKeyAsync();
        }
        _factory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public void AddServiceEndpoint(ServiceEndpoint endpoint)
    {
        if (endpoint.AccessKey is MicrosoftEntraAccessKey key)
        {
            _ = key.UpdateAccessKeyAsync();
        }
        _endpoints.TryAdd(endpoint, null);
    }

    public void Dispose() => _timer.Stop();

    public void UpdateServiceEndpoints(IEnumerable<ServiceEndpoint> endpoints)
    {
        _endpoints.Clear();
        foreach (var endpoint in endpoints)
        {
            AddServiceEndpoint(endpoint);
        }
    }

    internal bool ContainsServiceEndpoint(ServiceEndpoint e) => _endpoints.ContainsKey(e);

    internal int ServiceEndpointsCount() => _endpoints.Count;

    private async Task UpdateAccessKeyAsync()
    {
        using (_timer)
        {
            _timer.Start();

            while (await _timer)
            {
                foreach (var key in AccessKeyForMicrosoftEntraList)
                {
                    _ = key.UpdateAccessKeyAsync();
                }
            }
        }
    }

    private sealed class ReferenceEqualityComparer : IEqualityComparer<ServiceEndpoint>
    {
        internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        private ReferenceEqualityComparer()
        {
        }

        public bool Equals(ServiceEndpoint x, ServiceEndpoint y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(ServiceEndpoint obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
