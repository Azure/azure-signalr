﻿/*------------------------------------------------------------------------------
 * Modified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/6.22.0/src/Microsoft.IdentityModel.Tokens/Base64UrlEncoder.cs
 * Compared with original code
 *  1. Change class `Base64UrlEncoder` from `public` to `internal`
 *  2. Modify L68 ~ L75 in `internal static string Encode(byte[] inArray, int offset, int length)`
 *     Because we cannot access `LogMessages` and the old version of Package `Microsoft.IdentityModel.Logging` does not have method `MarkAsNonPII` for class `LogHelper`
 *  3. Modify L194 in `private unsafe static byte[] UnsafeDecode(string str)`
 *     The same reason as the first modification
------------------------------------------------------------------------------*/

using System;
using System.Text;

namespace Microsoft.Azure.SignalR
{
    /// <summary>
    /// Encodes and Decodes strings as Base64Url encoding.
    /// </summary>
    internal static class Base64UrlEncoder
    {
        private const char base64PadCharacter = '=';
#if NET45
        private const string doubleBase64PadCharacter = "==";
#endif
        private const char base64Character62 = '+';
        private const char base64Character63 = '/';
        private const char base64UrlCharacter62 = '-';
        private const char base64UrlCharacter63 = '_';

        /// <summary>
        /// Encoding table
        /// </summary>
        internal static readonly char[] s_base64Table =
        {
            'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
            'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z',
            '0','1','2','3','4','5','6','7','8','9',
            base64UrlCharacter62,
            base64UrlCharacter63
        };

        /// <summary>
        /// The following functions perform base64url encoding which differs from regular base64 encoding as follows
        /// * padding is skipped so the pad character '=' doesn't have to be percent encoded
        /// * the 62nd and 63rd regular base64 encoding characters ('+' and '/') are replace with ('-' and '_')
        /// The changes make the encoding alphabet file and URL safe.
        /// </summary>
        /// <param name="arg">string to encode.</param>
        /// <returns>Base64Url encoding of the UTF8 bytes.</returns>
        public static string Encode(string arg)
        {
            _ = arg ?? throw LogHelper.LogArgumentNullException(nameof(arg));

            return Encode(Encoding.UTF8.GetBytes(arg));
        }

        /// <summary>
        /// Converts a subset of an array of 8-bit unsigned integers to its equivalent string representation which is encoded with base-64-url digits. Parameters specify
        /// the subset as an offset in the input array, and the number of elements in the array to convert.
        /// </summary>
        /// <param name="inArray">An array of 8-bit unsigned integers.</param>
        /// <param name="length">An offset in inArray.</param>
        /// <param name="offset">The number of elements of inArray to convert.</param>
        /// <returns>The string representation in base 64 url encoding of length elements of inArray, starting at position offset.</returns>
        /// <exception cref="ArgumentNullException">'inArray' is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or length is negative OR offset plus length is greater than the length of inArray.</exception>
        public static string Encode(byte[] inArray, int offset, int length)
        {
            _ = inArray ?? throw LogHelper.LogArgumentNullException(nameof(inArray));

            if (length == 0)
                return string.Empty;

            // Modifications 1 starts from here
            if (length < 0)
                throw LogHelper.LogExceptionMessage(new ArgumentOutOfRangeException(LogHelper.FormatInvariant("IDX10106: The parameter {0} had an invalid value: '{1}'.", nameof(length), length)));
            
            if (offset < 0 || inArray.Length < offset)
                throw LogHelper.LogExceptionMessage(new ArgumentOutOfRangeException(LogHelper.FormatInvariant("IDX10106: The parameter {0} had an invalid value: '{1}'.", nameof(offset), offset)));

            if (inArray.Length < offset + length)
                throw LogHelper.LogExceptionMessage(new ArgumentOutOfRangeException(LogHelper.FormatInvariant("IDX10106: The parameter {0} had an invalid value: '{1}'.", nameof(length), length)));
            // Modifications 1 ends here

            int lengthmod3 = length % 3;
            int limit = offset + (length - lengthmod3);
            char[] output = new char[(length + 2) / 3 * 4];
            char[] table = s_base64Table;
            int i, j = 0;

            // takes 3 bytes from inArray and insert 4 bytes into output
            for (i = offset; i < limit; i += 3)
            {
                byte d0 = inArray[i];
                byte d1 = inArray[i + 1];
                byte d2 = inArray[i + 2];

                output[j + 0] = table[d0 >> 2];
                output[j + 1] = table[((d0 & 0x03) << 4) | (d1 >> 4)];
                output[j + 2] = table[((d1 & 0x0f) << 2) | (d2 >> 6)];
                output[j + 3] = table[d2 & 0x3f];
                j += 4;
            }

            //Where we left off before
            i = limit;

            switch (lengthmod3)
            {
                case 2:
                    {
                        byte d0 = inArray[i];
                        byte d1 = inArray[i + 1];

                        output[j + 0] = table[d0 >> 2];
                        output[j + 1] = table[((d0 & 0x03) << 4) | (d1 >> 4)];
                        output[j + 2] = table[(d1 & 0x0f) << 2];
                        j += 3;
                    }
                    break;

                case 1:
                    {
                        byte d0 = inArray[i];

                        output[j + 0] = table[d0 >> 2];
                        output[j + 1] = table[(d0 & 0x03) << 4];
                        j += 2;
                    }
                    break;

                    //default or case 0: no further operations are needed.
            }

            return new string(output, 0, j);
        }

        /// <summary>
        /// Converts a subset of an array of 8-bit unsigned integers to its equivalent string representation which is encoded with base-64-url digits.
        /// </summary>
        /// <param name="inArray">An array of 8-bit unsigned integers.</param>
        /// <returns>The string representation in base 64 url encoding of length elements of inArray, starting at position offset.</returns>
        /// <exception cref="ArgumentNullException">'inArray' is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or length is negative OR offset plus length is greater than the length of inArray.</exception>
        public static string Encode(byte[] inArray)
        {
            _ = inArray ?? throw LogHelper.LogArgumentNullException(nameof(inArray));

            return Encode(inArray, 0, inArray.Length);
        }

        public static string EncodeString(string str)
        {
            _ = str ?? throw LogHelper.LogArgumentNullException(nameof(str));

            return Encode(Encoding.UTF8.GetBytes(str));
        }

        /// <summary>
        ///  Converts the specified string, which encodes binary data as base-64-url digits, to an equivalent 8-bit unsigned integer array.</summary>
        /// <param name="str">base64Url encoded string.</param>
        /// <returns>UTF8 bytes.</returns>
        public static byte[] DecodeBytes(string str)
        {
            _ = str ?? throw LogHelper.LogExceptionMessage(new ArgumentNullException(nameof(str)));
#if NET45
            // 62nd char of encoding
            str = str.Replace(base64UrlCharacter62, base64Character62);
            
            // 63rd char of encoding
            str = str.Replace(base64UrlCharacter63, base64Character63);

            // check for padding
            switch (str.Length % 4)
            {
                case 0:
                    // No pad chars in this case
                    break;
                case 2:
                    // Two pad chars
                    str += doubleBase64PadCharacter;
                    break;
                case 3:
                    // One pad char
                    str += base64PadCharacter;
                    break;
                default:
                    throw LogHelper.LogExceptionMessage(new FormatException(LogHelper.FormatInvariant(LogMessages.IDX10400, str)));
            }

            return Convert.FromBase64String(str);
#else
            return UnsafeDecode(str);
#endif
        }

        /// <summary>
        /// Decodes the string from Base64UrlEncoded to UTF8.
        /// </summary>
        /// <param name="arg">string to decode.</param>
        /// <returns>UTF8 string.</returns>
        public static string Decode(string arg)
        {
            return Encoding.UTF8.GetString(DecodeBytes(arg));
        }

#if !NET45
        private unsafe static byte[] UnsafeDecode(string str)
        {
            int mod = str.Length % 4;
            if (mod == 1)
                // Modification 2 starts here
                throw LogHelper.LogExceptionMessage(new FormatException(LogHelper.FormatInvariant("IDX10400: Unable to decode: '{0}' as Base64url encoded string.", str)));
                // Modification 2 ends here

            bool needReplace = false;
            int decodedLength = str.Length + (4 - mod) % 4;

            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] == base64UrlCharacter62 || str[i] == base64UrlCharacter63)
                {
                    needReplace = true;
                    break;
                }
            }

            if (needReplace)
            {
                string decodedString = new string(char.MinValue, decodedLength);
                fixed (char* dest = decodedString)
                {
                    int i = 0;
                    for (; i < str.Length; i++)
                    {
                        if (str[i] == base64UrlCharacter62)
                            dest[i] = base64Character62;
                        else if (str[i] == base64UrlCharacter63)
                            dest[i] = base64Character63;
                        else
                            dest[i] = str[i];
                    }

                    for (; i < decodedLength; i++)
                        dest[i] = base64PadCharacter;
                }

                return Convert.FromBase64String(decodedString);
            }
            else
            {
                if (decodedLength == str.Length)
                {
                    return Convert.FromBase64String(str);
                }
                else
                {
                    string decodedString = new string(char.MinValue, decodedLength);
                    fixed (char* src = str)
                    fixed (char* dest = decodedString)
                    {
                        Buffer.MemoryCopy(src, dest, str.Length * 2, str.Length * 2);
                        dest[str.Length] = base64PadCharacter;
                        if (str.Length + 2 == decodedLength)
                            dest[str.Length + 1] = base64PadCharacter;
                    }

                    return Convert.FromBase64String(decodedString);
                }
            }
        }
#endif
    }
}