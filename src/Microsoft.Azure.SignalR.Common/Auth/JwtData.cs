// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Claims;
using System.Globalization;
using Microsoft.IdentityModel.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// A unified and simplified version of class JwtHeader and class JwtPayload
    /// </summary>
    /// Ref:
    ///     JwtHeader.cs: https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/799a162276cb699c6634f12077ab7af0646cf710/src/System.IdentityModel.Tokens.Jwt/JwtHeader.cs
    ///     JwtPayload.cs: https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/799a162276cb699c6634f12077ab7af0646cf710/src/System.IdentityModel.Tokens.Jwt/JwtPayload.cs
    public class JwtData:Dictionary<string, object>
    {
        private static char base64PadCharacter = '=';

        // private static string doubleBase64PadCharacter = "==";

        private static char base64Character62 = '+';

        private static char base64Character63 = '/';

        private static char base64UrlCharacter62 = '-';

        private static char _base64UrlCharacter63 = '_';


        /// <summary>
        /// Adds a JSON object representing the <see cref="Claim"/> to the <see cref="JwtData"/>
        /// </summary>
        /// Copied from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/f3ddfe0dcb40ec5bde3e324b4246984753a0e121/src/System.IdentityModel.Tokens.Jwt/JwtPayload.cs#L498
        public void AddClaim(Claim claim)
        {
            if (claim == null)
            {
                throw LogHelper.LogExceptionMessage(new ArgumentNullException("claim"));
            }

            AddClaims(new Claim[1] { claim });
        }

        /// <summary>
        /// Adds a number of <see cref="Claim"/> to the <see cref="JwtData"/> as JSON { name, value } pairs.
        /// </summary>
        /// <param name="claims">For each <see cref="Claim"/> a JSON pair { 'Claim.Type', 'Claim.Value' } is added. If duplicate claims are found then a { 'Claim.Type', List&lt;object&gt; } will be created to contain the duplicate values.</param>
        /// <remarks>
        /// <para>Any <see cref="Claim"/> in the <see cref="IEnumerable{Claim}"/> that is null, will be ignored.</para></remarks>
        /// <exception cref="ArgumentNullException"><paramref name="claims"/> is null.</exception>
        /// Copied from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/d6f2b66d788195b50f2b1f700beb497851194c73/src/System.IdentityModel.Tokens.Jwt/JwtPayload.cs#L513
        public void AddClaims(IEnumerable<Claim> claims)
        {
            if (claims == null)
            {
                throw LogHelper.LogExceptionMessage(new ArgumentNullException(nameof(claims)));
            }

            foreach (Claim claim in claims)
            {
                if (claim == null)
                {
                    continue;
                }

                string jsonClaimType = claim.Type;
                object jsonClaimValue = claim.ValueType.Equals(ClaimValueTypes.String, StringComparison.Ordinal) ? claim.Value : GetClaimValueUsingValueType(claim);
                object existingValue;

                // If there is an existing value, append to it.
                // What to do if the 'ClaimValueType' is not the same.
                if (TryGetValue(jsonClaimType, out existingValue))
                {
                    IList<object> claimValues = existingValue as IList<object>;
                    if (claimValues == null)
                    {
                        claimValues = new List<object>();
                        claimValues.Add(existingValue);
                        this[jsonClaimType] = claimValues;
                    }

                    claimValues.Add(jsonClaimValue);
                }
                else
                {
                    this[jsonClaimType] = jsonClaimValue;
                }
            }
        }

        // Copied From https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/d6f2b66d788195b50f2b1f700beb497851194c73/src/Microsoft.IdentityModel.Tokens/TokenUtilities.cs#L107
        internal static object GetClaimValueUsingValueType(Claim claim)
        {
            if (claim.ValueType == ClaimValueTypes.String)
                return claim.Value;

            if (claim.ValueType == ClaimValueTypes.Boolean && bool.TryParse(claim.Value, out bool boolValue))
                return boolValue;

            if (claim.ValueType == ClaimValueTypes.Double && double.TryParse(claim.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double doubleValue))
                return doubleValue;

            if ((claim.ValueType == ClaimValueTypes.Integer || claim.ValueType == ClaimValueTypes.Integer32) && int.TryParse(claim.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out int intValue))
                return intValue;

            if (claim.ValueType == ClaimValueTypes.Integer64 && long.TryParse(claim.Value, out long longValue))
                return longValue;

            if (claim.ValueType == ClaimValueTypes.DateTime && DateTime.TryParse(claim.Value, out DateTime dateTimeValue))
                return dateTimeValue;

            if (claim.ValueType == "JSON")
                return JObject.Parse(claim.Value);

            if (claim.ValueType == "JSON_ARRAY")
                return JArray.Parse(claim.Value);

            if (claim.ValueType == "JSON_NULL")
                return string.Empty;

            return claim.Value;
        }

        /// <summary>
        /// Convert <see cref="byte"/>[] to corresponding base64Url encoding
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        /// Modified from: https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/5810cd81dd3d7a802f111005f42990dc8ebb0aa7/src/Microsoft.IdentityModel.Tokens/Base64UrlEncoder.cs#L92
        public static string EncodeBytesToBase64Url(byte[] bytes)
        {
            if (bytes == null)
            {
                throw LogHelper.LogArgumentNullException("inArray");
            }
            return Convert.ToBase64String(bytes, 0, bytes.Length)
                .Split(base64PadCharacter)[0]
                .Replace(base64Character62, base64UrlCharacter62)
                .Replace(base64Character63, _base64UrlCharacter63);
        }

        /// <summary>
        /// Convert this <see cref="JwtData"/> to corresponding Base64Url encoding
        /// </summary>
        /// <returns>Base64Url encoding of this <see cref="JwtData"/></returns>
        /// Modified from: https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/799a162276cb699c6634f12077ab7af0646cf710/src/System.IdentityModel.Tokens.Jwt/JwtHeader.cs#L352
        public string GetBase64UrlEncoding()
        {
            string json = JsonExtensions.SerializeToJson(this);
            if (json == null) 
            {
                throw LogHelper.LogArgumentNullException("json");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(json);

            return EncodeBytesToBase64Url(bytes);
        }
    }
}
