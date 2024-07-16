/*------------------------------------------------------------------------------
 * Modified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtHeader.cs
 * Compared with original code
 *      1. Change class `JwtHeader` from `public` to `internal`
 *      2. Rewrite constructor of `JwtHeader` according to constructor `JwtHeader` in [JwtHeader.cs](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtHeader.cs#L84)
 *         Because the original signature encryption is too complex
 *      3. Simplify method `Base64UrlEncode`
------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    internal class JwtHeader : Dictionary<string, object>
    {
        /// <summary>
        /// Create a <see cref="JwtHeader"/> representing JWT header {"alg":<paramref name="algorithm"/>,"typ":"JWT","kid":<paramref name="kid"/>}
        /// </summary>
        /// <returns> JWT header</returns>
        public JwtHeader(string kid, AccessTokenAlgorithm algorithm)
        {
            // Write parameter `alg`
            switch (algorithm)
            {
                case AccessTokenAlgorithm.HS256:
                    this["alg"] = "HS256";
                    break;
                case AccessTokenAlgorithm.HS512:
                    this["alg"] = "HS512";
                    break;
                default:
                    throw new NotSupportedException("Not Supported Encryption Algorithm for JWT Token");
            }

            // Write parameter `typ` and `kid`
            this["typ"] = "JWT";
            if (kid != null)
            {
                this["kid"] = kid;
            }
        }

        /// Simplified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtHeader.cs#L328 and https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JsonExtensions.cs
        /// <summary>
        /// Convert this <see cref="JwtHeader"/> to corresponding Base64Url encoding
        /// </summary>
        /// <returns>Base64Url encoding of a <see cref="JwtHeader"/></returns>
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
