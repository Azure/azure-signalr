// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR;

#nullable enable

/// <summary>
/// Internal transport result returned after the service processes a <see cref="Protocol.RefreshAuthMessage"/>.
/// </summary>
internal readonly struct RefreshAuthResult
{
    public RefreshAuthResult(AckStatus status, IReadOnlyList<Claim>? claims = null, HubServiceEndpoint? owningEndpoint = null)
    {
        Status = status;
        Claims = claims;
        OwningEndpoint = owningEndpoint;
    }

    public AckStatus Status { get; }

    public IReadOnlyList<Claim>? Claims { get; }

    public HubServiceEndpoint? OwningEndpoint { get; }

    public void Deconstruct(out AckStatus status, out IReadOnlyList<Claim>? claims, out HubServiceEndpoint? owningEndpoint)
    {
        status = Status;
        claims = Claims;
        owningEndpoint = OwningEndpoint;
    }
}
