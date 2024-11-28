// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR;

internal class NegotiateHandler<THub> where THub : Hub
{
    private readonly IUserIdProvider _userIdProvider;

    private readonly IConnectionRequestIdProvider _connectionRequestIdProvider;

    private readonly Func<HttpContext, IEnumerable<Claim>> _claimsProvider;

    private readonly Func<HttpContext, bool> _diagnosticClientFilter;

    private readonly IServiceEndpointManager _endpointManager;

    private readonly IEndpointRouter _router;

    private readonly IBlazorDetector _blazorDetector;

    private readonly string _serverName;

    private readonly ServerStickyMode _mode;

    private readonly bool _enableDetailedErrors;

    private readonly int _endpointsCount;

    private readonly int? _maxPollInterval;

    private readonly int _customHandshakeTimeout;

    private readonly string _hubName;

    private readonly ILogger<NegotiateHandler<THub>> _logger;

    private readonly Func<HttpContext, HttpTransportType> _transportTypeDetector;

#if NET6_0_OR_GREATER
    private readonly HttpConnectionDispatcherOptions _dispatcherOptions;
#endif

    public NegotiateHandler(
        IOptions<HubOptions> globalHubOptions,
        IOptions<HubOptions<THub>> hubOptions,
        IServiceEndpointManager endpointManager,
        IEndpointRouter router,
        IUserIdProvider userIdProvider,
        IServerNameProvider nameProvider,
        IConnectionRequestIdProvider connectionRequestIdProvider,
        IOptions<ServiceOptions> options,
        IBlazorDetector blazorDetector,
#if NET6_0_OR_GREATER
        EndpointDataSource endpointDataSource,
#endif
        ILogger<NegotiateHandler<THub>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _endpointManager = endpointManager ?? throw new ArgumentNullException(nameof(endpointManager));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _serverName = nameProvider?.GetName();
        _userIdProvider = userIdProvider ?? throw new ArgumentNullException(nameof(userIdProvider));
        _connectionRequestIdProvider = connectionRequestIdProvider ?? throw new ArgumentNullException(nameof(connectionRequestIdProvider));
        _claimsProvider = options?.Value?.ClaimsProvider;
        _diagnosticClientFilter = options?.Value?.DiagnosticClientFilter;
        _blazorDetector = blazorDetector ?? new DefaultBlazorDetector();
        _mode = options.Value.ServerStickyMode;
        _enableDetailedErrors = globalHubOptions.Value.EnableDetailedErrors == true;
        _endpointsCount = options.Value.Endpoints.Length;
        _maxPollInterval = options.Value.MaxPollIntervalInSeconds;
        _transportTypeDetector = options.Value.TransportTypeDetector;
        _customHandshakeTimeout = GetCustomHandshakeTimeout(hubOptions.Value.HandshakeTimeout ?? globalHubOptions.Value.HandshakeTimeout);
        _hubName = typeof(THub).Name;
#if NET6_0_OR_GREATER
        _dispatcherOptions = GetDispatcherOptions(endpointDataSource, typeof(THub));
#endif
    }

    public async Task<NegotiationResponse> Process(HttpContext context)
    {
        var claims = BuildClaims(context);
        var request = context.Request;
        var cultureName = context.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture.Name;
        var uiCultureName = context.Features.Get<IRequestCultureFeature>()?.RequestCulture.UICulture.Name;
        var originalPath = GetOriginalPath(request.Path);
        var provider = _endpointManager.GetEndpointProvider(_router.GetNegotiateEndpoint(context, _endpointManager.GetEndpoints(_hubName)));

        if (provider == null)
        {
            return null;
        }

        var queryString = GetQueryString(
            request.QueryString.HasValue ? request.QueryString.Value.Substring(1) : null,
            cultureName,
            uiCultureName
        );

        return new NegotiationResponse
        {
            Url = provider.GetClientEndpoint(_hubName, originalPath, queryString),
            AccessToken = await provider.GenerateClientAccessTokenAsync(_hubName, claims),
            // Need to set this even though it's technically protocol violation https://github.com/aspnet/SignalR/issues/2133
            AvailableTransports = new List<AvailableTransport>()
        };
    }

    private static string GetOriginalPath(string path)
    {
        path = path.TrimEnd('/');
        return path.EndsWith(Constants.Path.Negotiate)
            ? path.Substring(0, path.Length - Constants.Path.Negotiate.Length)
            : string.Empty;
    }

    private string GetQueryString(string originalQueryString, string cultureName, string uiCultureName)
    {
        var clientRequestId = _connectionRequestIdProvider.GetRequestId();
        if (clientRequestId != null)
        {
            clientRequestId = WebUtility.UrlEncode(clientRequestId);
        }

        var queryString = $"{Constants.QueryParameter.ConnectionRequestId}={clientRequestId}";
        if (!string.IsNullOrEmpty(cultureName))
        {
            queryString += $"&{Constants.QueryParameter.RequestCulture}={cultureName}";
        }
        if (!string.IsNullOrEmpty(uiCultureName))
        {
            queryString += $"&{Constants.QueryParameter.RequestUICulture}={uiCultureName}";
        }

        return originalQueryString != null
            ? $"{originalQueryString}&{queryString}"
            : queryString;
    }

    private IEnumerable<Claim> BuildClaims(HttpContext context)
    {
        // Make sticky mode required if detect using blazor
        var mode = _blazorDetector.IsBlazor(_hubName) ? ServerStickyMode.Required : _mode;
        var userId = _userIdProvider.GetUserId(new ServiceHubConnectionContext(context));
        var httpTransportType = _transportTypeDetector?.Invoke(context);
        var closeOnAuthenticationExpiration = false;
        var authenticationExpiresOn = default(DateTimeOffset?);
#if NET6_0_OR_GREATER
        closeOnAuthenticationExpiration = _dispatcherOptions.CloseOnAuthenticationExpiration;
        var authResultFeature = context.Features.Get<IAuthenticateResultFeature>();
        if (authResultFeature != null && authResultFeature.AuthenticateResult != null && authResultFeature.AuthenticateResult.Succeeded)
        {
            authenticationExpiresOn = authResultFeature.AuthenticateResult.Properties.ExpiresUtc;
        }
#endif
        return ClaimsUtility.BuildJwtClaims(context.User,
                                            userId,
                                            GetClaimsProvider(context),
                                            _serverName,
                                            mode,
                                            _enableDetailedErrors,
                                            _endpointsCount,
                                            _maxPollInterval,
                                            IsDiagnosticClient(context),
                                            _customHandshakeTimeout,
                                            httpTransportType,
                                            closeOnAuthenticationExpiration,
                                            authenticationExpiresOn);
    }

    private Func<IEnumerable<Claim>> GetClaimsProvider(HttpContext context)
    {
        if (_claimsProvider == null)
        {
            return null;
        }
        return () => _claimsProvider.Invoke(context);
    }

    private bool IsDiagnosticClient(HttpContext context)
    {
        return _diagnosticClientFilter != null && _diagnosticClientFilter(context);
    }

    private int GetCustomHandshakeTimeout(TimeSpan? handshakeTimeout)
    {
        if (!handshakeTimeout.HasValue)
        {
            Log.UseDefaultHandshakeTimeout(_logger);
            return Constants.Periods.DefaultHandshakeTimeout;
        }

        var timeout = (int)handshakeTimeout.Value.TotalSeconds;

        // use default handshake timeout
        if (timeout == Constants.Periods.DefaultHandshakeTimeout)
        {
            Log.UseDefaultHandshakeTimeout(_logger);
            return Constants.Periods.DefaultHandshakeTimeout;
        }

        // the custom handshake timeout is invalid, use default hanshake timeout instead
        if (timeout <= 0 || timeout > Constants.Periods.MaxCustomHandshakeTimeout)
        {
            Log.FailToSetCustomHandshakeTimeout(_logger, new ArgumentOutOfRangeException(nameof(handshakeTimeout)));
            return Constants.Periods.DefaultHandshakeTimeout;
        }

        // the custom handshake timeout is valid
        Log.SucceedToSetCustomHandshakeTimeout(_logger, timeout);
        return timeout;
    }

#if NET6_0_OR_GREATER
    private static HttpConnectionDispatcherOptions GetDispatcherOptions(EndpointDataSource source, Type hubType)
    {
        foreach (var endpoint in source.Endpoints)
        {
            var metaData = endpoint.Metadata;
            if (metaData.GetMetadata<HubMetadata>()?.HubType == hubType)
            {
                var options = metaData.GetMetadata<HttpConnectionDispatcherOptions>();
                if (options != null)
                {
                    return options;
                }
            }
        }
        // It's not expected to go here in production environment. Return a value for test.
        return new();
    }
#endif

    private static class Log
    {
        private static readonly Action<ILogger, Exception> _useDefaultHandshakeTimeout =
            LoggerMessage.Define(LogLevel.Information, new EventId(0, "UseDefaultHandshakeTimeout"), "Use default handshake timeout.");

        private static readonly Action<ILogger, int, Exception> _succeedToSetCustomHandshakeTimeout =
            LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, "SucceedToSetCustomHandshakeTimeout"), "Succeed to set custom handshake timeout: {timeout} seconds.");

        private static readonly Action<ILogger, Exception> _failToSetCustomHandshakeTimeout =
            LoggerMessage.Define(LogLevel.Warning, new EventId(2, "FailToSetCustomHandshakeTimeout"), $"Fail to set custom handshake timeout, use default handshake timeout {Constants.Periods.DefaultHandshakeTimeout} seconds instead. The range of custom handshake timeout should between 1 second to {Constants.Periods.MaxCustomHandshakeTimeout} seconds.");

        public static void UseDefaultHandshakeTimeout(ILogger logger)
        {
            _useDefaultHandshakeTimeout(logger, null);
        }

        public static void SucceedToSetCustomHandshakeTimeout(ILogger logger, int customHandshakeTimeout)
        {
            _succeedToSetCustomHandshakeTimeout(logger, customHandshakeTimeout, null);
        }

        public static void FailToSetCustomHandshakeTimeout(ILogger logger, Exception exception)
        {
            _failToSetCustomHandshakeTimeout(logger, exception);
        }
    }
}