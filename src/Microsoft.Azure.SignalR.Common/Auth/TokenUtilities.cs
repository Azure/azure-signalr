// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*------------------------------------------------------------------------------
 * Simplified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/Microsoft.IdentityModel.Tokens/TokenUtilities.cs
 * Compared with the original code:
 *      1. Remove useless code
------------------------------------------------------------------------------*/

using System;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal class TokenUtilities
{
    internal const string Json = "JSON";

    internal const string JsonArray = "JSON_ARRAY";

    internal const string JsonNull = "JSON_NULL";

    internal static bool TryParseIssuer(string? token, out string? issuer)
    {
        issuer = null;
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var parts = token!.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var payloadJson = Base64UrlDecode(parts[1]);
        using var doc = JsonDocument.Parse(payloadJson);

        if (doc.RootElement.TryGetProperty("iss", out var value))
        {
            issuer = value.ToString();
            return true;
        }
        return false;
    }

    internal static object GetClaimValueUsingValueType(Claim claim)
    {
        if (claim.ValueType == ClaimValueTypes.String)
        {
            return claim.Value;
        }

        if (claim.ValueType == ClaimValueTypes.Boolean && bool.TryParse(claim.Value, out var boolValue))
        {
            return boolValue;
        }

        if (claim.ValueType == ClaimValueTypes.Double && double.TryParse(claim.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return doubleValue;
        }

        if ((claim.ValueType == ClaimValueTypes.Integer || claim.ValueType == ClaimValueTypes.Integer32) && int.TryParse(claim.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue;
        }

        if (claim.ValueType == ClaimValueTypes.Integer64 && long.TryParse(claim.Value, out var longValue))
        {
            return longValue;
        }

        if (claim.ValueType == ClaimValueTypes.DateTime && DateTime.TryParse(claim.Value, out var dateTimeValue))
        {
            return dateTimeValue;
        }

        if (claim.ValueType == Json)
        {
            return JObject.Parse(claim.Value);
        }

        if (claim.ValueType == JsonArray)
        {
            return JArray.Parse(claim.Value);
        }

        if (claim.ValueType == JsonNull)
        {
            return string.Empty;
        }

        return claim.Value;
    }

    private static string Base64UrlDecode(string base64Url)
    {
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var byteArray = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(byteArray);
    }
}
