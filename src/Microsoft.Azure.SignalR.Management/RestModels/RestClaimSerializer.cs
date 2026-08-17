// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.Azure.SignalR.Management;

#nullable enable

/// <summary>
/// Maps between <see cref="Claim"/> and the REST wire DTOs.
/// </summary>
internal static class RestClaimSerializer
{
    public static async Task<IReadOnlyList<Claim>> ReadClaimsAsync(HttpResponseMessage response)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        var body = await JsonSerializer.DeserializeAsync<ClaimsResponseBody>(stream);
        var dtos = body?.Claims;
        if (dtos == null || dtos.Length == 0)
        {
            return Array.Empty<Claim>();
        }
        var result = new Claim[dtos.Length];
        for (var i = 0; i < dtos.Length; i++)
        {
            result[i] = new Claim(dtos[i].Type, dtos[i].Value);
        }
        return result;
    }

    public static ClaimDto[]? ToClaimDtos(IEnumerable<Claim>? claims)
    {
        if (claims == null)
        {
            return null;
        }
        var list = new List<ClaimDto>();
        foreach (var claim in claims)
        {
            list.Add(new ClaimDto { Type = claim.Type, Value = claim.Value });
        }
        return list.Count == 0 ? null : list.ToArray();
    }
}
