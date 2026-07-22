// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Management;

/// <summary>
/// Shared implementation of the live-connection authentication refresh and claim-read operations,
/// used by both the non-generic and strongly-typed <see cref="ServiceHubContext"/> implementations.
/// </summary>
internal static class ConnectionAuthenticationHelper
{
    public static async Task<RefreshConnectionAuthenticationResult> RefreshConnectionAuthenticationAsync(
        string hubName,
        IServiceHubLifetimeManager lifetimeManager,
        IServiceEndpointManager endpointManager,
        string connectionToken,
        DateTimeOffset? expireTime,
        IEnumerable<Claim> claims,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(connectionToken))
        {
            throw new ArgumentException("Argument cannot be null or empty.", nameof(connectionToken));
        }

        var utcExpireTime = expireTime?.ToUniversalTime() ?? DateTimeOffset.MaxValue;

        if (expireTime.HasValue && utcExpireTime <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expireTime), "The expiration time must be in the future.");
        }

        var result = await lifetimeManager.RefreshAuthAsync(connectionToken, utcExpireTime, claims, cancellationToken);
        ThrowOnNonSuccess(result.Status);

        if (result.Claims == null || result.Claims.Count == 0)
        {
            throw new AzureSignalRException("The service did not return the post-refresh claim set required to mint the refreshed access token.");
        }

        var accessTokenLifetime = Constants.Periods.DefaultAccessTokenLifetime;
        var owningEndpoint = result.OwningEndpoint
            ?? throw new AzureSignalRException("Unexpected refresh result: The owning endpoint was not provided.");
        var provider = endpointManager.GetEndpointProvider(owningEndpoint);
        var accessToken = await provider.GenerateClientAccessTokenAsync(hubName, result.Claims, accessTokenLifetime);
        var remainingSeconds = Math.Max(0, (utcExpireTime - DateTimeOffset.UtcNow).TotalSeconds);
        var tokenLifetimeSeconds = (int)Math.Min(accessTokenLifetime.TotalSeconds, remainingSeconds);
        return new RefreshConnectionAuthenticationResult(accessToken, tokenLifetimeSeconds);
    }

    public static async Task<ConnectionClaims> GetConnectionClaimsAsync(
        IServiceHubLifetimeManager lifetimeManager,
        string connectionToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(connectionToken))
        {
            throw new ArgumentException("Argument cannot be null or empty.", nameof(connectionToken));
        }

        var result = await lifetimeManager.GetConnectionClaimsAsync(connectionToken, cancellationToken);
        ThrowOnNonSuccess(result.Status);
        return new ConnectionClaims(result.Claims ?? Array.Empty<Claim>());
    }

    private static void ThrowOnNonSuccess(AckStatus status)
    {
        switch (status)
        {
            case AckStatus.Ok:
                return;
            case AckStatus.Forbidden:
                throw new AzureSignalRException("The refreshed token resolves to a different user (ConnectionUserMismatch).");
            case AckStatus.NotFound:
                throw new AzureSignalRException("The target connection was not found, is disconnected, or uses an unsupported negotiate version.");
            case AckStatus.Timeout:
                throw new AzureSignalRException("The operation timed out before the service acknowledged it.");
            default:
                throw new AzureSignalRException("An unexpected error occurred while processing the request.");
        }
    }
}
