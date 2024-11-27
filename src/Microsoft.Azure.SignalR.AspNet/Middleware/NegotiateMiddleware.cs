// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Owin;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.AspNet;

internal class NegotiateMiddleware : OwinMiddleware
{
    private static readonly HashSet<string> PreservedQueryParameters =
        new HashSet<string>(new[] { "clientProtocol", "connectionToken", "connectionData" });

    private static readonly Version ClientSupportQueryStringVersion = new Version(2, 1);

    private static readonly string AssemblyVersion = typeof(NegotiateMiddleware).Assembly.GetName().Version.ToString();

    private readonly string _appName;

    private readonly Func<IOwinContext, IEnumerable<Claim>> _claimsProvider;

    private readonly Func<IOwinContext, bool> _diagnosticClientFilter;

    private readonly ILogger _logger;

    private readonly IServiceEndpointManager _endpointManager;

    private readonly IEndpointRouter _router;

    private readonly IConnectionRequestIdProvider _connectionRequestIdProvider;

    private readonly IUserIdProvider _provider;

    private readonly HubConfiguration _configuration;

    private readonly string _serverName;

    private readonly ServerStickyMode _mode;

    private readonly bool _enableDetailedErrors;

    private readonly int _endpointsCount;

    private readonly int? _maxPollInterval;

    public NegotiateMiddleware(OwinMiddleware next,
                               HubConfiguration configuration,
                               string appName,
                               IServiceEndpointManager endpointManager,
                               IEndpointRouter router,
                               ServiceOptions options,
                               IServerNameProvider serverNameProvider,
                               IConnectionRequestIdProvider connectionRequestIdProvider,
                               ILoggerFactory loggerFactory)
        : base(next)
    {
        _configuration = configuration;
        _provider = configuration.Resolver.Resolve<IUserIdProvider>();
        _appName = appName ?? throw new ArgumentNullException(nameof(appName));
        _claimsProvider = options?.ClaimsProvider;
        _diagnosticClientFilter = options?.DiagnosticClientFilter;
        _endpointManager = endpointManager ?? throw new ArgumentNullException(nameof(endpointManager));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _connectionRequestIdProvider = connectionRequestIdProvider ?? throw new ArgumentNullException(nameof(connectionRequestIdProvider));
        _logger = loggerFactory?.CreateLogger<NegotiateMiddleware>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        _serverName = serverNameProvider?.GetName();
        _mode = options.ServerStickyMode;
        _enableDetailedErrors = configuration.EnableDetailedErrors;
        _endpointsCount = options.Endpoints.Length;
        _maxPollInterval = options.MaxPollIntervalInSeconds;
    }

    public override Task Invoke(IOwinContext owinContext)
    {
        if (owinContext == null)
        {
            throw new ArgumentNullException(nameof(owinContext));
        }

        var context = new HostContext(owinContext.Environment);

        if (IsNegotiationRequest(context.Request))
        {
            return ProcessNegotiationRequest(owinContext, context);
        }

        // Trim any trailing slashes
        var normalized = context.Request.LocalPath.TrimEnd('/');

        if (normalized.EndsWith("/hubs", StringComparison.OrdinalIgnoreCase) ||
            normalized.EndsWith("/js", StringComparison.OrdinalIgnoreCase))
        {
            return Next.Invoke(owinContext);
        }

        context.Response.StatusCode = 404;
        return context.Response.End("NotFound");
    }

    private static string GetOriginalPath(string path)
    {
        path = path.TrimEnd('/');
        return path.EndsWith(Constants.Path.Negotiate)
            ? path.Substring(0, path.Length - Constants.Path.Negotiate.Length)
            : string.Empty;
    }

    private static string GetRedirectNegotiateResponse(string url, string token)
    {
        var sb = new StringBuilder();
        using (var jsonWriter = new JsonTextWriter(new StringWriter(sb)))
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("ProtocolVersion");
            jsonWriter.WriteValue("2.0");
            jsonWriter.WritePropertyName("RedirectUrl");
            jsonWriter.WriteValue(url);
            jsonWriter.WritePropertyName("AccessToken");
            jsonWriter.WriteValue(token);
            jsonWriter.WritePropertyName("TryWebSockets"); // fix the c# client issue https://github.com/SignalR/SignalR/issues/4435
            jsonWriter.WriteValue(true);
            jsonWriter.WriteEndObject();
        }

        return sb.ToString();
    }

    private static bool IsNegotiationRequest(IRequest request)
    {
        return request.LocalPath.EndsWith(Constants.Path.Negotiate, StringComparison.OrdinalIgnoreCase);
    }

    private static Task SendJsonResponse(HostContext context, string jsonPayload)
    {
        var callback = context.Request.QueryString["callback"];
        if (String.IsNullOrEmpty(callback))
        {
            // Send normal JSON response
            context.Response.ContentType = JsonUtility.JsonMimeType;
            return context.Response.End(jsonPayload);
        }

        // JSONP response is no longer supported.
        context.Response.StatusCode = 400;
        return context.Response.End("JSONP is no longer supported.");
    }

    private Task ProcessNegotiationRequest(IOwinContext owinContext, HostContext context)
    {
        var claims = BuildClaims(owinContext, context.Request);

        var dispatcher = new HubDispatcher(_configuration);
        try
        {
            dispatcher.Initialize(_configuration.Resolver);
            if (!dispatcher.Authorize(context.Request))
            {
                string error = null;
                if (context.Request.User != null && context.Request.User.Identity.IsAuthenticated)
                {
                    // If the user is authenticated and authorize failed then 403
                    error = "Forbidden";
                    context.Response.StatusCode = 403;
                }
                else
                {
                    // If failed to authorize the request then return 401
                    error = "Unauthorized";
                    context.Response.StatusCode = 401;
                }
                Log.NegotiateFailed(_logger, error);
                return context.Response.End(error);
            }
        }
        catch (Exception e)
        {
            Log.NegotiateFailed(_logger, e.Message);
            context.Response.StatusCode = 500;
            return context.Response.End("");
        }

        IServiceEndpointProvider provider;
        try
        {
            // Take the service endpoints for the app
            provider = _endpointManager.GetEndpointProvider(_router.GetNegotiateEndpoint(owinContext, _endpointManager.GetEndpoints(_appName)));

            // When status code changes, we consider the inner router changed the response, then we stop here
            if (context.Response.StatusCode != 200)
            {
                // Inner handler already write to context.Response, no need to continue with error case
                return Task.CompletedTask;
            }

            // Consider it as internal server error when we don't successfully get negotiate response
            if (provider == null)
            {
                var message = "Unable to get the negotiate endpoint";
                Log.NegotiateFailed(_logger, message);
                context.Response.StatusCode = 500;
                return context.Response.End(message);
            }
        }
        catch (AzureSignalRNotConnectedException e)
        {
            Log.NegotiateFailed(_logger, e.Message);
            context.Response.StatusCode = 500;
            return context.Response.End(e.Message);
        }

        // Redirect to Service
        var clientProtocol = context.Request.QueryString["clientProtocol"];
        string originalPath = null;
        string queryString = null;

        // add OriginalPath and QueryString when the clients protocol is higher than 2.0, earlier ASP.NET SignalR clients does not support redirect URL with query parameters
        if (!string.IsNullOrEmpty(clientProtocol) && Version.TryParse(clientProtocol, out var version) && version >= ClientSupportQueryStringVersion)
        {
            var clientRequestId = _connectionRequestIdProvider.GetRequestId();
            if (clientRequestId != null)
            {
                // remove system preserved query strings
                queryString = "?" +
                    string.Join("&",
                        context.Request.QueryString.Where(s => !PreservedQueryParameters.Contains(s.Key)).Concat(
                                new[]
                                {
                                    new KeyValuePair<string, string>(
                                        Constants.QueryParameter.ConnectionRequestId,
                                        clientRequestId)
                                })
                            .Select(s => $"{Uri.EscapeDataString(s.Key)}={Uri.EscapeDataString(s.Value)}"));
            }

            originalPath = GetOriginalPath(context.Request.LocalPath);
        }

        var url = provider.GetClientEndpoint(null, originalPath, queryString);
        try
        {
            return GenerateClientAccessTokenAsync(provider, context, url, claims);
        }
        catch (AzureSignalRAccessTokenNotAuthorizedException e)
        {
            Log.NegotiateFailed(_logger, e.Message);
            context.Response.StatusCode = 500;
            return context.Response.End("");
        }
    }

    private async Task GenerateClientAccessTokenAsync(
        IServiceEndpointProvider provider,
        HostContext context,
        string url,
        IEnumerable<Claim> claims)
    {
        try
        {
            var accessToken = await provider.GenerateClientAccessTokenAsync(null, claims);
            await SendJsonResponse(context, GetRedirectNegotiateResponse(url, accessToken));
        }
        catch (AzureSignalRAccessTokenTooLongException ex)
        {
            Log.NegotiateFailed(_logger, ex.Message);
            context.Response.StatusCode = 413;
            await context.Response.End(ex.Message);
        }
    }

    private IEnumerable<Claim> BuildClaims(IOwinContext owinContext, IRequest request)
    {
        // Pass appname through jwt token to client, so that when client establishes connection with service, it will also create a corresponding AppName-connection
        yield return new Claim(Constants.ClaimType.AppName, _appName);
        var user = owinContext.Authentication?.User;
        var userId = _provider?.GetUserId(request);
        var claims = ClaimsUtility.BuildJwtClaims(user, userId, GetClaimsProvider(owinContext), _serverName, _mode, _enableDetailedErrors, _endpointsCount, _maxPollInterval, IsDiagnosticClient(owinContext));

        yield return new Claim(Constants.ClaimType.Version, AssemblyVersion);

        foreach (var claim in claims)
        {
            yield return claim;
        }
    }

    private Func<IEnumerable<Claim>> GetClaimsProvider(IOwinContext context)
    {
        if (_claimsProvider == null)
        {
            return null;
        }

        return () => _claimsProvider.Invoke(context);
    }

    private bool IsDiagnosticClient(IOwinContext context)
    {
        return _diagnosticClientFilter != null && _diagnosticClientFilter(context);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception> _negotiateFailed =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "NegotiateFailed"), "Client negotiate failed: {Error}");

        public static void NegotiateFailed(ILogger logger, string error)
        {
            _negotiateFailed(logger, error, null);
        }
    }
}
