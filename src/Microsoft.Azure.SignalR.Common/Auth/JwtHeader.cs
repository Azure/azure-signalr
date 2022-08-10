/*------------------------------------------------------------------------------
 * Modified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/v6.15.0/src/System.IdentityModel.Tokens.Jwt/JwtHeader.cs
 * Compared with original code
 *      1. Rewrite constructor of `JwtHeader` according to constructor `JwtHeader` in [JwtHeader.cs](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtHeader.cs#L84)
 *         Because the original signature encryption is too complex
 *      2. Simplify method `Base64UrlEncode`
------------------------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Logging;

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

        /// <summary>
        /// Convert this <see cref="JwtHeader"/> to corresponding Base64Url encoding
        /// </summary>
        /// <returns>Base64Url encoding of a <see cref="JwtHeader"/></returns>
        /// Simplified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/System.IdentityModel.Tokens.Jwt/JwtHeader.cs#L328
        public string Base64UrlEncode()
        {
            string json = JsonExtensions.SerializeToJson(this as IDictionary<string, object>);
            if (json == null)
            {
                throw LogHelper.LogArgumentNullException("json");
            }

            byte[] bytes = Encoding.UTF8.GetBytes(json);

            return Base64UrlEncoder.Encode(bytes);
        }
    }
}
