// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class AccessKeySynchronizer : IAccessKeySynchronizer, IDisposable
    {
        private readonly ConcurrentDictionary<ServiceEndpoint, object> _endpoints = new ConcurrentDictionary<ServiceEndpoint, object>(ReferenceEqualityComparer.Instance);

        private readonly ILoggerFactory _factory;
        private readonly TimerAwaitable _timer = new TimerAwaitable(TimeSpan.Zero, TimeSpan.FromMinutes(1));

        public AccessKeySynchronizer(
            ILoggerFactory loggerFactory
            ) : this(loggerFactory, true)
        {
        }

        /// <summary>
        /// For test only.
        /// </summary>
        internal AccessKeySynchronizer(
            ILoggerFactory loggerFactory,
            bool start
        )
        {
            if (start)
            {
                _ = UpdateAccessKeyAsync();
            }
            _factory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        public void AddServiceEndpoint(ServiceEndpoint endpoint)
        {
            if (endpoint.AccessKey is AadAccessKey aadKey)
            {
                _ = UpdateAccessKeyAsync(aadKey);
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

        internal IEnumerable<AadAccessKey> FilterAadAccessKeys() => _endpoints.Select(e => e.Key.AccessKey).OfType<AadAccessKey>();

        private async Task UpdateAccessKeyAsync()
        {
            using (_timer)
            {
                _timer.Start();

                while (await _timer)
                {
                    foreach (var key in FilterAadAccessKeys())
                    {
                        _ = UpdateAccessKeyAsync(key);
                    }
                }
            }
        }

        private async Task UpdateAccessKeyAsync(AadAccessKey key)
        {
            var logger = _factory.CreateLogger<AadAccessKey>();
            try
            {
                await key.UpdateAccessKeyAsync();
                Log.SucceedToAuthorizeAccessKey(logger, key.Endpoint.AbsoluteUri);
            }
            catch (Exception e)
            {
                Log.FailedToAuthorizeAccessKey(logger, key.Endpoint.AbsoluteUri, e);
            }
        }


        private sealed class ReferenceEqualityComparer : IEqualityComparer<ServiceEndpoint>
        {
            internal static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            private ReferenceEqualityComparer() { }

            public bool Equals(ServiceEndpoint x, ServiceEndpoint y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(ServiceEndpoint obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _failedAuthorize =
                LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "FailedAuthorizeAccessKey"), "Failed in authorizing AccessKey for '{endpoint}', will retry in " + AadAccessKey.AuthorizeRetryIntervalInSec + " seconds");

            private static readonly Action<ILogger, string, Exception> _succeedAuthorize =
                LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "SucceedAuthorizeAccessKey"), "Succeed in authorizing AccessKey for '{endpoint}'");

            public static void FailedToAuthorizeAccessKey(ILogger logger, string endpoint, Exception e)
            {
                _failedAuthorize(logger, endpoint, e);
            }

            public static void SucceedToAuthorizeAccessKey(ILogger logger, string endpoint)
            {
                _succeedAuthorize(logger, endpoint, null);
            }
        }
    }
}
