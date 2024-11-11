// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR;

internal class ServiceEndpointManager : ServiceEndpointManagerBase
{
    private readonly ILogger _logger;

    // Store the initial ServiceOptions for generating EndpointProvider use.
    // Only Endpoints value accept hot-reload and prevent changes of unexpected modification on other configurations.
    private readonly ServiceOptions _options;

    private readonly TimeSpan _scaleTimeout;

    private readonly IAccessKeySynchronizer _synchronizer;

    public ServiceEndpointManager(IAccessKeySynchronizer synchronizer,
                                  IOptionsMonitor<ServiceOptions> optionsMonitor,
                                  ILoggerFactory loggerFactory) :
        base(optionsMonitor.CurrentValue, loggerFactory.CreateLogger<ServiceEndpointManager>())
    {
        _options = optionsMonitor.CurrentValue;
        _logger = loggerFactory?.CreateLogger<ServiceEndpointManager>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        _synchronizer = synchronizer;

        optionsMonitor.OnChange(OnChange);
        _scaleTimeout = _options.ServiceScaleTimeout;
    }

    public override IServiceEndpointProvider GetEndpointProvider(ServiceEndpoint endpoint)
    {
        if (endpoint == null)
        {
            return null;
        }

        _synchronizer.AddServiceEndpoint(endpoint);
        return new ServiceEndpointProvider(endpoint, _options);
    }

    private void OnChange(ServiceOptions options)
    {
        Log.DetectConfigurationChanges(_logger);
        ReloadServiceEndpointsAsync(ServiceEndpointUtility.Merge(options.ConnectionString, options.Endpoints));
    }

    private Task ReloadServiceEndpointsAsync(IEnumerable<ServiceEndpoint> serviceEndpoints)
    {
        _synchronizer.UpdateServiceEndpoints(serviceEndpoints);
        return ReloadServiceEndpointsAsync(serviceEndpoints, _scaleTimeout);
    }

    private static class Log
    {
        private static readonly Action<ILogger, Exception> _detectEndpointChanges =
            LoggerMessage.Define(LogLevel.Debug, new EventId(1, "DetectConfigurationChanges"), "Dected configuration changes in configuration, start live-scale.");

        public static void DetectConfigurationChanges(ILogger logger)
        {
            _detectEndpointChanges(logger, null);
        }
    }
}
