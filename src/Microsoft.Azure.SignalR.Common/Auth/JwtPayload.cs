/*------------------------------------------------------------------------------
 * Modified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtPayload.cs
 * Compared with original code
    *  1. Change class `JwtPayload` from `public` to `internal`
    *  2. Rewrite constructor for class `JwtPayload`. Details are commented above the constructor
    *  3. Remove useless code
------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Claims;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR
{
    internal class JwtPayload : Dictionary<string, object>
    {
        /*
         * Modified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtPayload.cs#L87
         * Compared with original code
         *  1. Remove input parameter `claimsCollection`. 
         *     Because we can ensure it equals `null` when generating JWT token in Azure SignalR
         *  2. Remove L93, L94 to AddDictionaryClaims. 
         *     Same reason as 1.
         *  3. Merge method `AddFirstPriorityClaims` to this constructor 
         *  4. Modify L107 in method `AddFirstPriorityClaims`. 
         *     Because we cannot access `LogMessages` and the old version of Package `Microsoft.IdentityModel.Logging` does not have method `MarkAsNonPII` for class `LogHelper`
         */
        public JwtPayload(string issuer = null, string audience = null, IEnumerable<Claim> claims = null, DateTime? notBefore = null, DateTime? expires = null, DateTime? issuedAt = null)
        {
            if (claims != null)
                AddClaims(claims);

            if (expires.HasValue)
            {
                if (notBefore.HasValue)
                {
                    if (notBefore.Value >= expires.Value)
                    {
                        throw LogHelper.LogExceptionMessage(new ArgumentException(LogHelper.FormatInvariant("IDX12401: Expires: '{0}' must be after NotBefore: '{1}'.", expires.Value, notBefore.Value)));
                    }

                    this[JwtRegisteredClaimNames.Nbf] = EpochTime.GetIntDate(notBefore.Value.ToUniversalTime());
                }

                this[JwtRegisteredClaimNames.Exp] = EpochTime.GetIntDate(expires.Value.ToUniversalTime());
            }

            if (issuedAt.HasValue)
                this[JwtRegisteredClaimNames.Iat] = EpochTime.GetIntDate(issuedAt.Value.ToUniversalTime());

            if (!string.IsNullOrEmpty(issuer))
                this[JwtRegisteredClaimNames.Iss] = issuer;

            // if could be the case that some of the claims above had an 'aud' claim;
            if (!string.IsNullOrEmpty(audience))
                AddClaim(new Claim(JwtRegisteredClaimNames.Aud, audience, ClaimValueTypes.String));
        }

        // Copied from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtPayload.cs#L474
        public void AddClaim(Claim claim)
        {
            if (claim == null)
            {
                throw LogHelper.LogExceptionMessage(new ArgumentNullException("claim"));
            }

            AddClaims(new Claim[1] { claim });
        }

        // Modified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtPayload.cs#L489 <summary>
        // Modification:
        //     object jsonClaimValue = claim.ValueType.Equals(ClaimValueTypes.String, StringComparison.Ordinal) ? claim.Value : TokenUtilities.GetClaimValueUsingValueType(claim);
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
                object jsonClaimValue = claim.ValueType.Equals(ClaimValueTypes.String, StringComparison.Ordinal) ? claim.Value : TokenUtilities.GetClaimValueUsingValueType(claim);
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

        // Simplified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtPayload.cs#L761 and https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JsonExtensions.cs
        public string Base64UrlEncode()
        {
            string json = JsonConvert.SerializeObject(this as IDictionary<string, object>);
            if (json == null)
            {
                throw LogHelper.LogArgumentNullException("json");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(json);

            return Base64UrlEncoder.Encode(bytes);
        }
    }
}
