using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR
{
    internal class AccessKeyManager : IAccessKeyManager
    {
        private readonly TimerAwaitable _timer = new TimerAwaitable(TimeSpan.Zero, TimeSpan.FromMinutes(5));

        private readonly ConcurrentDictionary<HubServiceEndpoint, object> _endpoints = new ConcurrentDictionary<HubServiceEndpoint, object>();

        public AccessKeyManager(
            IServerNameProvider nameProvider,
            ILoggerFactory loggerFactory
            )
        {
            _ = UpdateAsync(nameProvider, loggerFactory);
        }

        private async Task UpdateAsync(IServerNameProvider provider, ILoggerFactory factory)
        {
            _timer.Start();

            while (await _timer)
            {
                var keys = new Dictionary<AadAccessKey, object>();
                foreach (var entity in _endpoints)
                {
                    if (entity.Key.AccessKey is AadAccessKey aadKey)
                    {
                        keys.Add(aadKey, null);
                    }
                }

                foreach (var entity in keys)
                {
                    _ = entity.Key.UpdateAccessKeyAsync(provider, factory);
                }
            }
        }

        public void AddHubServiceEndpoint(HubServiceEndpoint endpoint)
        {
            _endpoints.TryAdd(endpoint, null);
        }

        public void RemoveHubServiceEndpoint(HubServiceEndpoint endpoint)
        {
            _endpoints.TryRemove(endpoint, out _);
        }
    }
}
