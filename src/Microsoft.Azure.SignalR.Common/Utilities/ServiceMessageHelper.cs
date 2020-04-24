// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Cryptography;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal static class ServiceMessageHelper
    {
        private static readonly RNGCryptoServiceProvider _keyGenerator = new RNGCryptoServiceProvider();
        private static readonly char[] _padding = { '=' };

        // todo: generate a shorter ID
        public static string GenerateMessageId()
        {
            // 128 bit buffer / 8 bits per byte = 16 bytes
            Span<byte> buffer = stackalloc byte[16];
            var bufferArray = buffer.ToArray();
            _keyGenerator.GetBytes(bufferArray);
            // Generate the id with RNGCrypto because we want a cryptographically random id, which GUID is not
            return Base64UrlEncode(bufferArray);
        }

        public static string GetMessageId(ServiceMessage serviceMessage)
        {
            if (serviceMessage is MulticastDataMessage multicastDataMessage)
            {
                return multicastDataMessage.MessageId;
            }

            return null;
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert
                .ToBase64String(input)
                .TrimEnd(_padding)
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}