﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceEndpointManager : ServiceEndpointManagerBase
    {
        private readonly ILogger _logger;

        // Store the initial ServiceOptions for generating EndpointProvider use.
        // Only Endpoints value accept hot-reload and prevent changes of unexpected modification on other configurations.
        private readonly ServiceOptions _options;
        private readonly TimeSpan _scaleTimeout;
        private IReadOnlyList<ServiceEndpoint> _endpointsStore;

        public ServiceEndpointManager(IOptionsMonitor<ServiceOptions> optionsMonitor, ILoggerFactory loggerFactory) :
            base(optionsMonitor.CurrentValue, loggerFactory.CreateLogger<ServiceEndpointManager>())
        {
            if (Endpoints.Length == 0)
            {
                throw new ArgumentException(ServiceEndpointProvider.ConnectionStringNotFound);
            }
            _options = optionsMonitor.CurrentValue;
            _logger = loggerFactory?.CreateLogger<ServiceEndpointManager>() ?? throw new ArgumentNullException(nameof(loggerFactory));

            // TODO: Enable optionsMonitor.OnChange when feature ready.
            // optionsMonitor.OnChange(OnChange);
            _endpointsStore = Endpoints;
            _scaleTimeout = _options.ServiceScaleTimeout;
        }

        public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
        {
            if (endpoint == null)
            {
                return null;
            }

            return new ServiceEndpointProvider(endpoint, _options);
        }

        private async void OnChange(ServiceOptions options)
        {
            Log.DetectConfigurationChanges(_logger);

            // Reset local cache and validate result
            var endpoints = GetValuableEndpoints(GetEndpoints(options));
            if (endpoints.Length == 0)
            {
                Log.EndpointNotFound(_logger);
                return; 
            }
            Endpoints = endpoints;

            var updatedEndpoints = GetChangedEndpoints(Endpoints);

            await RenameSerivceEndpoints(updatedEndpoints.RenamedEndpoints);

            using (var addCts = new CancellationTokenSource(options.ServiceScaleTimeout))
            {
                if (!await WaitTaskOrTimeout(AddServiceEndpointsAsync(updatedEndpoints.AddedEndpoints, addCts.Token), addCts))
                {
                    Log.TimeoutAddEndpoints(_logger);
                }
            }

            using (var removeCts = new CancellationTokenSource(options.ServiceScaleTimeout))
            {
                if (!await WaitTaskOrTimeout(RemoveServiceEndpointsAsync(updatedEndpoints.RemovedEndpoints, removeCts.Token), removeCts))
                {
                    Log.TimeoutRemoveEndpoints(_logger);
                }
            }

            _endpointsStore = Endpoints;
        }

        private (IReadOnlyList<ServiceEndpoint> AddedEndpoints, 
            IReadOnlyList<ServiceEndpoint> RemovedEndpoints,
            IReadOnlyList<ServiceEndpoint> RenamedEndpoints)
            GetChangedEndpoints(IEnumerable<ServiceEndpoint> updatedEndpoints)
        {
            var originalEndpoints = _endpointsStore;
            var addedEndpoints = updatedEndpoints.Except(originalEndpoints, new ServiceEndpointWeakComparer()).ToList();
            var removedEndpoints = originalEndpoints.Except(updatedEndpoints, new ServiceEndpointWeakComparer()).ToList();

            var renamedEndpoints = updatedEndpoints.Except(originalEndpoints).Except(addedEndpoints).ToList();

            return (AddedEndpoints: addedEndpoints, RemovedEndpoints: removedEndpoints, RenamedEndpoints: renamedEndpoints);
        }

        private static async Task<bool> WaitTaskOrTimeout(Task task, CancellationTokenSource cts)
        {
            var completed = await Task.WhenAny(task, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token));

            if (completed == task)
            {
                return true;
            }

            cts.Cancel();
            return false;
        }

        private sealed class ServiceEndpointWeakComparer : IEqualityComparer<ServiceEndpoint>
        {
            public bool Equals(ServiceEndpoint x, ServiceEndpoint y)
            {
                return x.Endpoint == y.Endpoint && x.EndpointType == y.EndpointType;
            }

            public int GetHashCode(ServiceEndpoint obj)
            {
                return obj.Endpoint.GetHashCode() ^ obj.EndpointType.GetHashCode();
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, Exception> _detectEndpointChanges =
                LoggerMessage.Define(LogLevel.Debug, new EventId(1, "DetectConfigurationChanges"), "Dected configuration changes in configuration, start live-scale.");

            private static readonly Action<ILogger, Exception> _endpointNotFound =
                LoggerMessage.Define(LogLevel.Warning, new EventId(2, "EndpointNotFound"), "No connection string is specified. Skip scale operation.");

            private static readonly Action<ILogger, Exception> _timeoutRenameEndpoints =
                LoggerMessage.Define(LogLevel.Error, new EventId(3, "TimeoutRenameEndpoints"), "Timeout waiting for renaming endpoints.");

            private static readonly Action<ILogger, Exception> _timeoutAddEndpoints =
                LoggerMessage.Define(LogLevel.Error, new EventId(4, "TimeoutAddEndpoints"), "Timeout waiting for adding endpoints.");
            
            private static readonly Action<ILogger, Exception> _timeoutRemoveEndpoints =
                LoggerMessage.Define(LogLevel.Error, new EventId(5, "TimeoutRemoveEndpoints"), "Timeout waiting for removing endpoints.");

            public static void DetectConfigurationChanges(ILogger logger)
            {
                _detectEndpointChanges(logger, null);
            }

            public static void EndpointNotFound(ILogger logger)
            {
                _endpointNotFound(logger, null);
            }

            public static void TimeoutRenameEndpoints(ILogger logger)
            {
                _timeoutRenameEndpoints(logger, null);
            }

            public static void TimeoutAddEndpoints(ILogger logger)
            {
                _timeoutAddEndpoints(logger, null);
            }

            public static void TimeoutRemoveEndpoints(ILogger logger)
            {
                _timeoutRemoveEndpoints(logger, null);
            }
        }
    }
}
