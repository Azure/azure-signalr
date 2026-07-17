// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET11_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

/// <summary>
/// Handler for the /refresh endpoint, which mints a refreshed access token for an existing connection and sends a RefreshAuthMessage to the runtime.
/// </summary>
internal class RefreshHandler<THub> where THub : Hub
{
    private readonly NegotiateHandler<THub> _negotiateHandler;

    private readonly IServiceConnectionManager<THub> _serviceConnectionManager;

    private readonly ILogger<RefreshHandler<THub>> _logger;

    public RefreshHandler(
        NegotiateHandler<THub> negotiateHandler,
        IServiceConnectionManager<THub> serviceConnectionManager,
        ILogger<RefreshHandler<THub>> logger)
    {
        _negotiateHandler = negotiateHandler ?? throw new ArgumentNullException(nameof(negotiateHandler));
        _serviceConnectionManager = serviceConnectionManager ?? throw new ArgumentNullException(nameof(serviceConnectionManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ProcessAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        if (!_negotiateHandler.IsAuthenticationRefreshEnabled)
        {
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "refresh_disabled");
            return;
        }

        var connectionToken = context.Request.Query["id"].ToString();
        if (string.IsNullOrEmpty(connectionToken))
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "missing_connection_token");
            return;
        }

        var newUser = context.User;
        if (newUser?.Identity?.IsAuthenticated != true)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "invalid_token");
            return;
        }

        if (NegotiateHandler<THub>.HasWindowsIdentity(newUser))
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "windows_identity_not_supported");
            return;
        }

        var newExpiration = _negotiateHandler.GetRefreshExpiration(context);

        // When an OnAuthenticationRefresh callback is configured, fetch PreviousUser from the runtime.
        // The endpoint that answers the read is remembered so the refresh below can be pinned to it
        // instead of fanning out to every endpoint a second time.
        HubServiceEndpoint owningEndpoint = null;
        var callback = _negotiateHandler.AuthenticationRefreshCallback;
        if (callback != null)
        {
            var (claimsStatus, previousClaims, answeringEndpoint) = await _serviceConnectionManager.GetConnectionClaimsAsync(
                new GetConnectionClaimsMessage(connectionToken, 0), context.RequestAborted);
            switch (claimsStatus)
            {
                case AckStatus.Ok:
                    owningEndpoint = answeringEndpoint;
                    break;
                case AckStatus.NotFound:
                    await WriteErrorAsync(context, StatusCodes.Status404NotFound, "connection_not_found");
                    return;
                default:
                    Log.RefreshFailed(_logger, connectionToken, claimsStatus);
                    await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "internal_server_error");
                    return;
            }

            var refreshContext = new AuthenticationRefreshContext
            {
                HttpContext = context,
                // Server doesn't know the public connection id
                ConnectionId = string.Empty,
                PreviousUser = BuildPreviousUser(previousClaims),
                NewUser = newUser,
                NewExpiration = newExpiration,
            };

            if (!await callback(refreshContext))
            {
                await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_change_rejected");
                return;
            }
        }

        var projectedClaims = _negotiateHandler.BuildRefreshClaims(context);
        var claims = projectedClaims.Length == 0 ? null : projectedClaims;

        AckStatus refreshStatus;
        RefreshAuthResult refreshResult;
        try
        {
            refreshResult = await _serviceConnectionManager.RefreshAuthAsync(
                new RefreshAuthMessage(connectionToken, claims, newExpiration, 0), owningEndpoint, context.RequestAborted);
        }
        catch (Exception ex)
        {
            Log.RefreshServiceCallFailed(_logger, ex);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "internal_server_error");
            return;
        }

        refreshStatus = refreshResult.Status;
        switch (refreshStatus)
        {
            case AckStatus.Ok:
                break;
            case AckStatus.NotFound:
                await WriteErrorAsync(context, StatusCodes.Status404NotFound, "connection_not_found");
                return;
            case AckStatus.Forbidden:
                await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "permission_change_rejected");
                return;
            default:
                Log.RefreshFailed(_logger, connectionToken, refreshStatus);
                await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "internal_server_error");
                return;
        }

        // Mint the refreshed access token only after ASRS confirms the refresh.
        if (refreshResult.Claims == null || refreshResult.Claims.Count == 0 || refreshResult.OwningEndpoint == null)
        {
            Log.RefreshMissingClaimPayload(_logger, connectionToken);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "internal_server_error");
            return;
        }

        try
        {
            var (accessToken, tokenLifetimeSeconds) = await _negotiateHandler.GenerateRefreshedAccessTokenAsync(
                refreshResult.OwningEndpoint, refreshResult.Claims);
            await WriteSuccessAsync(context, accessToken, tokenLifetimeSeconds);
        }
        catch (Exception ex)
        {
            Log.RefreshTokenGenerationFailed(_logger, ex);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "internal_server_error");
        }
    }

    /// <summary>
    /// Reconstructs a claims-only ClaimsPrincipal from the service-token claim set returned by the runtime.
    /// </summary>
    private static ClaimsPrincipal BuildPreviousUser(IReadOnlyList<Claim> serviceClaims)
    {
        if (serviceClaims == null || serviceClaims.Count == 0)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var authenticationType = "Bearer";
        var nameType = ClaimsIdentity.DefaultNameClaimType;
        var roleType = ClaimsIdentity.DefaultRoleClaimType;
        var appClaims = new List<Claim>(serviceClaims.Count);

        foreach (var claim in serviceClaims)
        {
            switch (claim.Type)
            {
                case Constants.ClaimType.AuthenticationType:
                    authenticationType = claim.Value;
                    break;
                case Constants.ClaimType.NameType:
                    nameType = claim.Value;
                    break;
                case Constants.ClaimType.RoleType:
                    roleType = claim.Value;
                    break;
                default:
                    if (claim.Type.StartsWith(Constants.ClaimType.AzureSignalRUserPrefix, StringComparison.Ordinal))
                    {
                        appClaims.Add(new Claim(claim.Type.Substring(Constants.ClaimType.AzureSignalRUserPrefix.Length), claim.Value));
                    }
                    else if (!claim.Type.StartsWith(Constants.ClaimType.AzureSignalRSysPrefix, StringComparison.Ordinal))
                    {
                        appClaims.Add(claim);
                    }
                    break;
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(appClaims, authenticationType, nameType, roleType));
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string error)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new RefreshErrorResponse { Error = error }));
    }

    private static Task WriteSuccessAsync(HttpContext context, string accessToken, int tokenLifetimeSeconds)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new RefreshSuccessResponse
        {
            AccessToken = accessToken,
            TokenLifetimeSeconds = tokenLifetimeSeconds,
        }));
    }

    private sealed class RefreshErrorResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public string Error { get; set; }
    }

    private sealed class RefreshSuccessResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("accessToken")]
        public string AccessToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("tokenLifetimeSeconds")]
        public int TokenLifetimeSeconds { get; set; }
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, AckStatus, Exception> _refreshFailed =
            LoggerMessage.Define<string, AckStatus>(LogLevel.Warning, new EventId(1, "RefreshAuthFailed"), "Refresh auth for connection token {ConnectionToken} failed with status {Status}.");

        private static readonly Action<ILogger, Exception> _refreshServiceCallFailed =
            LoggerMessage.Define(LogLevel.Error, new EventId(2, "RefreshAuthServiceCallFailed"), "Refresh auth failed while communicating with the Azure SignalR service.");

        private static readonly Action<ILogger, Exception> _refreshTokenGenerationFailed =
            LoggerMessage.Define(LogLevel.Error, new EventId(3, "RefreshAuthTokenGenerationFailed"), "Failed to generate the refreshed access token after the service confirmed the refresh.");

        private static readonly Action<ILogger, string, Exception> _refreshMissingClaimPayload =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, "RefreshAuthMissingClaimPayload"), "The service confirmed the refresh for connection token {ConnectionToken} but returned no claim payload; the runtime does not understand the refresh contract. Rejecting.");

        public static void RefreshFailed(ILogger logger, string connectionToken, AckStatus status) => _refreshFailed(logger, connectionToken, status, null);

        public static void RefreshServiceCallFailed(ILogger logger, Exception ex) => _refreshServiceCallFailed(logger, ex);

        public static void RefreshTokenGenerationFailed(ILogger logger, Exception ex) => _refreshTokenGenerationFailed(logger, ex);

        public static void RefreshMissingClaimPayload(ILogger logger, string connectionToken) => _refreshMissingClaimPayload(logger, connectionToken, null);
    }
}
#endif
