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
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR;

internal class TokenUtilities
{
    internal const string Json = "JSON";

    internal const string JsonArray = "JSON_ARRAY";

    internal const string JsonNull = "JSON_NULL";

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
}
