/*------------------------------------------------------------------------------
 * Copied from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/Microsoft.IdentityModel.JsonWebTokens/JwtRegisteredClaimNames.cs
------------------------------------------------------------------------------*/

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// List of registered claims from different sources
    /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
    /// http://openid.net/specs/openid-connect-core-1_0.html#IDToken
    /// </summary>
    internal struct JwtRegisteredClaimNames
    {
        /// <summary>
        /// </summary>
        internal const string Actort = "actort";

        /// <summary>
        /// http://openid.net/specs/openid-connect-core-1_0.html#IDToken
        /// </summary>
        internal const string Acr = "acr";

        /// <summary>
        /// http://openid.net/specs/openid-connect-core-1_0.html#IDToken
        /// </summary>
        internal const string Amr = "amr";

        /// <summary>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        internal const string Aud = "aud";

        /// <summary>
        /// http://openid.net/specs/openid-connect-core-1_0.html#IDToken
        /// </summary>
        internal const string AuthTime = "auth_time";

        /// <summary>
        /// http://openid.net/specs/openid-connect-core-1_0.html#IDToken
        /// </summary>
        internal const string Azp = "azp";

        /// <summary>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        internal const string Birthdate = "birthdate";

        /// <summary>
        /// https://openid.net/specs/openid-connect-core-1_0.html#HybridIDToken
        /// </summary>
        internal const string CHash = "c_hash";

        /// <summary>
        /// http://openid.net/specs/openid-connect-core-1_0.html#CodeIDToken
        /// </summary>
        internal const string AtHash = "at_hash";

        /// <summary>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        internal const string Email = "email";

        /// <summary>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        internal const string Exp = "exp";

        /// <summary>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        internal const string Gender = "gender";

        /// <summary>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        internal const string FamilyName = "family_name";

        /// <summary>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        internal const string GivenName = "given_name";

        /// <summary>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        internal const string Iat = "iat";

        /// <summary>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        internal const string Iss = "iss";

        /// <summary>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        internal const string Jti = "jti";

        /// <summary>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        internal const string Name = "name";

        /// <summary>
        /// </summary>
        internal const string NameId = "nameid";

        /// <summary>
        /// https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest
        /// </summary>
        internal const string Nonce = "nonce";

        /// <summary>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        internal const string Nbf = "nbf";

        /// <summary>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        internal const string PhoneNumber = "phone_number";

        /// <summary>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        internal const string PhoneNumberVerified = "phone_number_verified";

        /// <summary>
        /// </summary>
        internal const string Prn = "prn";

        /// <summary>
        /// http://openid.net/specs/openid-connect-frontchannel-1_0.html#OPLogout
        /// </summary>
        internal const string Sid = "sid";

        /// <summary>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        internal const string Sub = "sub";

        /// <summary>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-5
        /// </summary>
        internal const string Typ = "typ";

        /// <summary>
        /// </summary>
        internal const string UniqueName = "unique_name";

        /// <summary>
        /// </summary>
        internal const string Website = "website";
    }
}
