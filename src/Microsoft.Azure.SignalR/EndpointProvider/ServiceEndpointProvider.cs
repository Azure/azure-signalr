// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointProvider : IServiceEndpointProvider, IDisposable
    {
        public static readonly string ConnectionStringNotFound =
            "No connection string was specified. " +
            $"Please specify a configuration entry for {Constants.Keys.ConnectionStringDefaultKey}, " +
            "or explicitly pass one using IServiceCollection.AddAzureSignalR(connectionString) in Startup.ConfigureServices.";

        private const int AuthorizeRetryIntervalInSec = 3;
        private const int AuthorizeIntervalInMinute = 55;
        private const int AuthorizeMaxRetryTimes = 3;

        private static readonly TimeSpan AuthorizeRetryInterval = TimeSpan.FromSeconds(AuthorizeRetryIntervalInSec);
        private static readonly TimeSpan AuthorizeInterval = TimeSpan.FromMinutes(AuthorizeIntervalInMinute);
        private static readonly TimeSpan AuthorizeTimeout = TimeSpan.FromSeconds(10);

        private readonly AccessKey _accessKey;
        private readonly string _appName;
        private readonly TimeSpan _accessTokenLifetime;
        private readonly IServiceEndpointGenerator _generator;
        private readonly AccessTokenAlgorithm _algorithm;

        public IWebProxy Proxy { get; }

        private readonly TimerAwaitable _timer = new TimerAwaitable(TimeSpan.Zero, AuthorizeInterval);

        public ServiceEndpointProvider(
            IServerNameProvider provider,
            ServiceEndpoint endpoint,
            ServiceOptions serviceOptions,
            ILoggerFactory loggerFactory = null
        )
        {
            _accessTokenLifetime = serviceOptions.AccessTokenLifetime;
            _accessKey = endpoint.AccessKey;
            _appName = serviceOptions.ApplicationName;
            _algorithm = serviceOptions.AccessTokenAlgorithm;

            Proxy = serviceOptions.Proxy;

            var port = endpoint.Port;
            var version = endpoint.Version;

            _generator = new DefaultServiceEndpointGenerator(endpoint.Endpoint, version, port);

            _ = UpdateAccessKeyAsync(provider, endpoint, loggerFactory ?? NullLoggerFactory.Instance);
        }

        public async Task UpdateAccessKeyAsync(IServerNameProvider provider, ServiceEndpoint endpoint, ILoggerFactory loggerFactory)
        {
            if (endpoint.AccessKey is AadAccessKey key)
                _timer.Start();
            else
                return;

            var logger = loggerFactory.CreateLogger<ServiceEndpointProvider>();

            while (await _timer)
            {
                var isAuthorized = false;
                for (int i = 0; i < AuthorizeMaxRetryTimes; i++)
                {
                    logger.LogInformation($"Try authorizing AccessKey...({i})");
                    var source = new CancellationTokenSource(AuthorizeTimeout);
                    try
                    {
                        await key.AuthorizeAsync(endpoint.Endpoint, endpoint.Port, provider.GetName(), source.Token);
                        logger.LogInformation("AccessKey has been authorized successfully.");
                        isAuthorized = true;
                        break;
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(e, $"Failed to authorize AccessKey, will retry in {AuthorizeRetryIntervalInSec} seconds.");
                        await Task.Delay(AuthorizeRetryInterval);
                    }
                }

                if (!isAuthorized)
                {
                    logger.LogError($"AccessKey authorized failed more than {AuthorizeMaxRetryTimes} times.");
                }
            }
        }

        public Task<string> GenerateClientAccessTokenAsync(string hubName, IEnumerable<Claim> claims = null, TimeSpan? lifetime = null)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var audience = _generator.GetClientAudience(hubName, _appName);

            return _accessKey.GenerateAccessToken(audience, claims, lifetime ?? _accessTokenLifetime, _algorithm);
        }

        public Task<string> GenerateServerAccessTokenAsync(string hubName, string userId, TimeSpan? lifetime = null)
        {
            // if (_accessKey is AadAccessKey key)
            // {
            //     return key.GenerateAadToken();
            // }

            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            var audience = _generator.GetServerAudience(hubName, _appName);
            var claims = userId != null ? new[] { new Claim(ClaimTypes.NameIdentifier, userId) } : null;

            return _accessKey.GenerateAccessToken(audience, claims, lifetime ?? _accessTokenLifetime, _algorithm);
        }

        public string GetClientEndpoint(string hubName, string originalPath, string queryString)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetClientEndpoint(hubName, _appName, originalPath, queryString);
        }

        public string GetServerEndpoint(string hubName)
        {
            if (string.IsNullOrEmpty(hubName))
            {
                throw new ArgumentNullException(nameof(hubName));
            }

            return _generator.GetServerEndpoint(hubName, _appName);
        }

        public void Dispose()
        {
            ((IDisposable)_timer).Dispose();
        }
    }
}
