// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Security.Cryptography;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal static class ServiceMessageHelper
    {
        private const int BufferLength = 8;
        private static readonly RNGCryptoServiceProvider _keyGenerator = new RNGCryptoServiceProvider();
        private static readonly char[] _padding = { '=' };

        public static string GenerateMessageIdPrefix()
        {
            // 64 bit buffer / 8 bits per byte = 8 bytes
            var buffer = new byte[BufferLength];
            _keyGenerator.GetBytes(buffer);
            // Generate the id with RNGCrypto because we want a cryptographically random id, which GUID is not
            return Base64UrlEncode(buffer);
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