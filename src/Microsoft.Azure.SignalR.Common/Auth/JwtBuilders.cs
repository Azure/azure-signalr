// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Security.Claims;
using Newtonsoft.Json.Bson;
using System.Linq.Expressions;

namespace Microsoft.Azure.SignalR
{
    /* Modified from https://github.com/Azure/azure-sdk-for-net/blob/2999288f431649dcbf54450b7ab237e45c953978/sdk/webpubsub/Azure.Messaging.WebPubSub/src/JwtBuilder.cs
     * Modifications are as follows:
     *      1. This new implementation supports variable header {"alg":<alg>, "typ":"JWT", "kid":<kid>} while the original version uses a fixed header {"alg":"HS256","typ":"JWT"}
     *      2. Create a new method `public void AddClaims(IEnumerable<Claim> claims`
     *          (a) Follows the logic of https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/b1814f6013262793512be596016d5c75bc5d4fea/src/System.IdentityModel.Tokens.Jwt/JwtPayload.cs#L513
     *          (b) ATTENTION: ONLY supports `Claim.Value` which is string. The original version can handle different                    `ClaimValueType`
     */

    internal class JwtBuilder : IDisposable
    {
        // Registered claims
        private static byte[] s_nbf = Encoding.UTF8.GetBytes("nbf");
        private static byte[] s_exp = Encoding.UTF8.GetBytes("exp");
        private static byte[] s_iat = Encoding.UTF8.GetBytes("iat");
        private static byte[] s_aud = Encoding.UTF8.GetBytes("aud");
        private static byte[] s_sub = Encoding.UTF8.GetBytes("sub");
        private static byte[] s_iss = Encoding.UTF8.GetBytes("iss");
        private static byte[] s_jti = Encoding.UTF8.GetBytes("jti");

        public static ReadOnlySpan<byte> Nbf => s_nbf;
        public static ReadOnlySpan<byte> Exp => s_exp;
        public static ReadOnlySpan<byte> Iat => s_iat;
        public static ReadOnlySpan<byte> Aud => s_aud;
        public static ReadOnlySpan<byte> Sub => s_sub;
        public static ReadOnlySpan<byte> Iss => s_iss;
        public static ReadOnlySpan<byte> Jti => s_jti;

        private byte[] headerSha256;

        private Utf8JsonWriter _writer;
        private MemoryStream _memoryStream;
        private byte[] _key;
        private bool _isDisposed;

        private byte[] _jwt;
        private int _jwtLength;
        private AccessTokenAlgorithm _algorithm;

        public JwtBuilder(byte[] key, int size = 512, string kid = null, AccessTokenAlgorithm algorithm = AccessTokenAlgorithm.HS256)
        { // typical JWT is ~300B UTF8
            _jwt = null;
            _memoryStream = new MemoryStream(size);

            headerSha256 = GenerateHeaderSha256(kid, algorithm, size);
            _memoryStream.Write(headerSha256, 0, headerSha256.Length);

            _writer = new Utf8JsonWriter(_memoryStream);
            _writer.WriteStartObject();
            _key = key;
            _algorithm = algorithm;
        }

        /// <summary>
        /// Returns Base64 encoding of the JWT header {"alg":<paramref name="algorithm"/>,"typ":"JWT","kid":<paramref name="kid"/>} and a dot ('.') as the delimiter. 
        /// Sample Input  : <paramref name="algorithm"/>=HS256, <paramref name="kid"/>="2091071007", 
        /// Sample Output : Encoding.ASCII.GetBytes("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCIsImtpZCI6IjIwOTEwNzEwMDcifQ.")
        /// </summary>
        /// <returns></returns>
        private byte[] GenerateHeaderSha256(string kid, AccessTokenAlgorithm algorithm, int maxSize = 512)
        {
            // Build header json via `_header_writer`
            MemoryStream _memoryStream = new MemoryStream(maxSize);
            var _header_writer = new Utf8JsonWriter(_memoryStream);
            _header_writer.WriteStartObject();

            // Write parameter `alg`
            switch (algorithm)
            {
                case AccessTokenAlgorithm.HS256:
                    _header_writer.WriteString("alg", "HS256");
                    break;
                case AccessTokenAlgorithm.HS512:
                    _header_writer.WriteString("alg", "HS512");
                    break;
                default:
                    break;
            }
            // Write parameter `typ` and `kid`
            _header_writer.WriteString("typ", "JWT");
            if (kid != null)
            {
                _header_writer.WriteString("kid", kid);
            }

            _header_writer.WriteEndObject();
            _header_writer.Flush();

            // Get lengths of the header json string and the corresponding Base64 string
            int headerLength = (int)_header_writer.BytesCommitted;
            int headerBase64Length = Base64.GetMaxEncodedToUtf8Length(headerLength) + 1;    // append a dot '.' to its tail

            // Make room to prepare for in-place Base64 conversion 
            byte[] _headerSha256 = new byte[headerBase64Length];
            // Copy header json string to `_headerSha256`
            Array.Copy(_memoryStream.GetBuffer(), _headerSha256, headerLength);
            // in-place conversion from json string to Base64
            Span<byte> toEncode = _headerSha256.AsSpan(0);
            OperationStatus status = NS2Bridge.Base64UrlEncodeInPlace(toEncode, headerLength, out var _headerWritten);

            // Buffer is adjusted above, and so encoding should always fit
            Debug.Assert(status == OperationStatus.Done);
            // The last character is a dot '.' as the delimiter
            _headerSha256[headerBase64Length - 1] = (byte)'.';

            return _headerSha256;
        }

        public void AddClaim(ReadOnlySpan<byte> utf8Name, string value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            _writer.WriteString(utf8Name, value);
        }
        public void AddClaim(ReadOnlySpan<byte> utf8Name, bool value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            _writer.WriteBoolean(utf8Name, value);
        }
        public void AddClaim(ReadOnlySpan<byte> utf8Name, long value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            _writer.WriteNumber(utf8Name, value);
        }
        public void AddClaim(ReadOnlySpan<byte> utf8Name, double value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            _writer.WriteNumber(utf8Name, value);
        }
        public void AddClaim(ReadOnlySpan<byte> utf8Name, DateTimeOffset value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            AddClaim(utf8Name, value.ToUnixTimeSeconds());
        }
        public void AddClaim(ReadOnlySpan<byte> utf8Name, IEnumerable<string> value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            _writer.WriteStartArray(utf8Name);
            foreach (var item in value)
            {
                _writer.WriteStringValue(item);
            }
            _writer.WriteEndArray();
        }

        public void AddClaim(string name, string value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            _writer.WriteString(name, value);
        }
        public void AddClaim(string name, bool value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            _writer.WriteBoolean(name, value);
        }
        public void AddClaim(string name, long value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            _writer.WriteNumber(name, value);
        }
        public void AddClaim(string name, double value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            _writer.WriteNumber(name, value);
        }
        public void AddClaim(string name, DateTimeOffset value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            AddClaim(name, value.ToUnixTimeSeconds());
        }
        public void AddClaim(string name, string[] value)
        {
            if (_writer == null)
                throw new InvalidOperationException("Cannot change claims after building. Create a new JwtBuilder instead");
            _writer.WriteStartArray(name);
            foreach (var item in value)
            {
                _writer.WriteStringValue(item);
            }
            _writer.WriteEndArray();
        }

        /// <summary>
        /// Returns number of ASCII characters of the JTW. The actual token can be retrieved using Build or WriteTo
        /// </summary>
        /// <returns></returns>
        public int End()
        {
            if (_writer == null) return _jwtLength; // writer is set to null after token is formatted.
            if (_isDisposed) throw new ObjectDisposedException(nameof(JwtBuilder));

            _writer.WriteEndObject();
            _writer.Flush();

            Debug.Assert(_memoryStream.GetType() == typeof(MemoryStream));
            int payloadLength = (int)_writer.BytesCommitted; // writer is wrrapping MemoryStream, and so the length will never overflow int.

            int payloadIndex = headerSha256.Length;

            int maxBufferLength;
            checked
            {
                maxBufferLength =
                    Base64.GetMaxEncodedToUtf8Length(headerSha256.Length + payloadLength)
                    + 1 // dot
                    + Base64.GetMaxEncodedToUtf8Length(32); // signature SHA256 hash size
            }
            _memoryStream.Capacity = maxBufferLength; // make room for in-place Base64 conversion

            _jwt = _memoryStream.GetBuffer();
            _writer = null; // this will prevent subsequent additions of claims.

            Span<byte> toEncode = _jwt.AsSpan(payloadIndex);
            OperationStatus status = NS2Bridge.Base64UrlEncodeInPlace(toEncode, payloadLength, out int payloadWritten);
            Debug.Assert(status == OperationStatus.Done); // Buffer is adjusted above, and so encoding should always fit

            // Add signature
            int headerAndPayloadLength = payloadWritten + headerSha256.Length;
            _jwt[headerAndPayloadLength] = (byte)'.';
            int headerAndPayloadAndSeparatorLength = headerAndPayloadLength + 1;

            byte[] hashed;
            HMAC hash = null;
            switch (_algorithm)
            {
                case AccessTokenAlgorithm.HS256:
                    hash = new HMACSHA256(_key);
                    break;
                case AccessTokenAlgorithm.HS512:
                    hash = new HMACSHA512(_key);
                    break;
                default:
                    break;
            }
            hashed = hash.ComputeHash(_jwt, 0, headerAndPayloadLength);
            status = NS2Bridge.Base64UrlEncode(hashed, _jwt.AsSpan(headerAndPayloadAndSeparatorLength), out int consumend, out int signatureLength);
            Debug.Assert(status == OperationStatus.Done); // Buffer is adjusted above, and so encoding should always fit
            _jwtLength = headerAndPayloadAndSeparatorLength + signatureLength;

            return _jwtLength;
        }

        public bool TryBuildTo(Span<char> destination, out int charsWritten)
        {
            End();
            if (destination.Length < _jwtLength)
            {
                charsWritten = 0;
                return false;
            }
            NS2Bridge.Latin1ToUtf16(_jwt.AsSpan(0, _jwtLength), destination);
            charsWritten = _jwtLength;
            return true;
        }

        public string BuildString()
        {
            End();
            var result = NS2Bridge.CreateString(_jwtLength, _jwt, (destination, state) => {
                NS2Bridge.Latin1ToUtf16(state.AsSpan(0, _jwtLength), destination);
            });
            return result;
        }

        // Modified from https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/blob/b1814f6013262793512be596016d5c75bc5d4fea/src/System.IdentityModel.Tokens.Jwt/JwtPayload.cs#L513
        // ATTENTION: This method ONLY supports `Claim.Value` which is string. The original implementation can handle different `ClaimValueType`
        public void AddClaims(IEnumerable<Claim> claims)
        {
            if (claims == null) return;
            Dictionary<string, List<string>> keyClaims = new Dictionary<string, List<string>>();
            foreach (var claim in claims)
            {
                if (claim == null)
                {
                    continue;
                }
                if (keyClaims.ContainsKey(claim.Type))
                {
                    keyClaims[claim.Type].Add(claim.Value);
                }
                else
                {
                    List<string> list = new List<string>() { claim.Value };
                    keyClaims.Add(claim.Type, list);
                }
            }
            foreach (var keyClaim in keyClaims)
            {
                if (keyClaim.Value.Count > 1)
                {
                    AddClaim(keyClaim.Key, keyClaim.Value.ToArray());
                }
                else
                {
                    AddClaim(keyClaim.Key, keyClaim.Value[0]);
                }
            }
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    if (_memoryStream != null)
                        _memoryStream.Dispose();
                    if (_writer != null)
                        _writer.Dispose();
                }

                _memoryStream = null;
                _writer = null;
                _key = null;
                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }
    }

    internal static class NS2Bridge
    {
        public delegate void SpanAction<T, TArg>(Span<T> buffer, TArg state);
        public static string CreateString<TState>(int length, TState state, SpanAction<char, TState> action)
        {
            var result = new string((char)0, length);
            unsafe
            {
                fixed (char* chars = result)
                {
                    var charBuffer = new Span<char>(chars, result.Length);
                    action(charBuffer, state);
                }
            }
            return result;
        }

        public static void Latin1ToUtf16(ReadOnlySpan<byte> latin1, Span<char> utf16)
        {
            if (utf16.Length < latin1.Length)
                throw new ArgumentOutOfRangeException(nameof(utf16));
            for (int i = 0; i < latin1.Length; i++)
            {
                utf16[i] = (char)latin1[i];
            }
        }

        public static OperationStatus Base64UrlEncodeInPlace(Span<byte> buffer, long dataLength, out int bytesWritten)
        {
            OperationStatus status = Base64.EncodeToUtf8InPlace(buffer, (int)dataLength, out bytesWritten);
            if (status != OperationStatus.Done)
            {
                return status;
            }

            bytesWritten = Base64ToBase64Url(buffer.Slice(0, bytesWritten));
            return OperationStatus.Done;
        }
        public static OperationStatus Base64UrlEncode(ReadOnlySpan<byte> buffer, Span<byte> destination, out int bytesConsumend, out int bytesWritten)
        {
            OperationStatus status = Base64.EncodeToUtf8(buffer, destination, out bytesConsumend, out bytesWritten, isFinalBlock: true);
            if (status != OperationStatus.Done)
            {
                return status;
            }

            bytesWritten = Base64ToBase64Url(destination.Slice(0, bytesWritten));
            return OperationStatus.Done;
        }

        private static int Base64ToBase64Url(Span<byte> buffer)
        {
            var bytesWritten = buffer.Length;
            if (buffer[bytesWritten - 1] == (byte)'=')
            {
                bytesWritten--;
                if (buffer[bytesWritten - 1] == (byte)'=')
                    bytesWritten--;
            }
            for (int i = 0; i < bytesWritten; i++)
            {
                byte current = buffer[i];
                if (current == (byte)'+')
                    buffer[i] = (byte)'-';
                else if (current == (byte)'/')
                    buffer[i] = (byte)'_';
            }
            return bytesWritten;
        }
    }

    internal class KeyBytesCache
    {
        public KeyBytesCache(string key)
        {
            Key = key;
            KeyBytes = Encoding.UTF8.GetBytes(key);
        }
        public readonly byte[] KeyBytes;
        public readonly string Key;
    }
}
