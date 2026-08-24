// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.Azure.SignalR;

#nullable enable

/// <summary>
/// The result of reading the claims of a live client connection, including the endpoint that answered
/// so a subsequent refresh can be pinned to it instead of fanning out again.
/// </summary>
internal readonly struct GetConnectionClaimsResult
{
    public GetConnectionClaimsResult(AckStatus status, IReadOnlyList<Claim>? claims = null, HubServiceEndpoint? owningEndpoint = null)
    {
        Status = status;
        Claims = claims;
        OwningEndpoint = owningEndpoint;
    }

    public AckStatus Status { get; }

    public IReadOnlyList<Claim>? Claims { get; }

    public HubServiceEndpoint? OwningEndpoint { get; }

    public void Deconstruct(out AckStatus status, out IReadOnlyList<Claim>? claims)
    {
        status = Status;
        claims = Claims;
    }

    public void Deconstruct(out AckStatus status, out IReadOnlyList<Claim>? claims, out HubServiceEndpoint? owningEndpoint)
    {
        status = Status;
        claims = Claims;
        owningEndpoint = OwningEndpoint;
    }
}
