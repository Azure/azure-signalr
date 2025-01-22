// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace Microsoft.Azure.SignalR;

internal interface IConnectionRequestIdProvider
{
    string GetRequestId(string traceIdentifier);
}

internal class DefaultConnectionRequestIdProvider : IConnectionRequestIdProvider
{
    private int _nextId;
    private int NextId() => Interlocked.Increment(ref _nextId);

    public string GetRequestId(string traceIdentifier)
    { 
        // Before filled into query string, this id will be process by "WebUtility.UrlEncode(...)". So base64 encoding is not needed.
        // Use hex to shorten the length.
        var id = NextId();
        var suffix = GetHex(CombineHashCodes(id, id ^ 0x12345678));
        return string.IsNullOrEmpty(traceIdentifier)
            ? $"{suffix}"
            : $"{traceIdentifier.Replace(":", "-")}-{suffix}";
    }

    private static string GetHex(int value)
    {
#if NET6_0_OR_GREATER
        Span<byte> span = stackalloc byte[4];
        BitConverter.TryWriteBytes(span, value);
        return Convert.ToHexString(span);
#else
        return value.ToString("X");
#endif
    }

    internal static int CombineHashCodes(int h1, int h2)
    {
#if NET6_0_OR_GREATER
        return HashCode.Combine(h1, h2);
#else
        // Ref: https://referencesource.microsoft.com/#System.Web/Util/HashCodeCombiner.cs,37
        return ((h1 << 5) + h1) ^ h2;
#endif
    }
}
