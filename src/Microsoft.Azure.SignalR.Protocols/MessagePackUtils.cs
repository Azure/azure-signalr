// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using MessagePack;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Azure.SignalR.Protocol;
internal static class MessagePackUtils
{
    internal static readonly IDictionary<string, ReadOnlyMemory<byte>> EmptyReadOnlyMemoryDictionary = new Dictionary<string, ReadOnlyMemory<byte>>();

    internal static readonly IDictionary<string, StringValues> EmptyStringValuesDictionaryIgnoreCase = new Dictionary<string, StringValues>(StringComparer.OrdinalIgnoreCase);

    internal static readonly int ProtocolVersion = 1;

    internal static Claim[] ReadClaims(ref MessagePackReader reader)
    {
        var claimCount = ReadMapLength(ref reader, "claims");
        if (claimCount > 0)
        {
            var claims = new Claim[claimCount];

            for (var i = 0; i < claimCount; i++)
            {
                var type = ReadString(ref reader, "claims[{0}].Type", i);
                var value = ReadString(ref reader, "claims[{0}].Value", i);
                claims[i] = new Claim(type, value);
            }

            return claims;
        }

        return [];
    }

    internal static IDictionary<string, ReadOnlyMemory<byte>> ReadPayloads(ref MessagePackReader reader)
    {
        var payloadCount = ReadMapLength(ref reader, "payloads");
        if (payloadCount > 0)
        {
            var payloads = new ArrayDictionary<string, ReadOnlyMemory<byte>>((int)payloadCount, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < payloadCount; i++)
            {
                var keyName = $"payloads[{i}].key";

                var key = ReadStringNotNull(ref reader, keyName);
                var value = ReadBytes(ref reader, "payloads[{0}].value", i);
                payloads.Add(key, value);
            }

            return payloads;
        }

        return EmptyReadOnlyMemoryDictionary;
    }

    internal static IDictionary<string, StringValues> ReadHeaders(ref MessagePackReader reader)
    {
        var headerCount = ReadMapLength(ref reader, "headers");
        if (headerCount > 0)
        {
            var headers = new Dictionary<string, StringValues>((int)headerCount, StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < headerCount; i++)
            {
                var keyName = $"headers[{i}].key";
                var key = ReadStringNotNull(ref reader, keyName);
                var count = ReadArrayLength(ref reader, $"headers[{i}].value.length");
                var stringValues = new string?[count];
                for (var j = 0; j < count; j++)
                {
                    stringValues[j] = ReadString(ref reader, $"headers[{i}].value[{j}]");
                }
                headers.Add(key, stringValues);
            }

            return headers;
        }

        return EmptyStringValuesDictionaryIgnoreCase;
    }

    internal static bool ReadBoolean(ref MessagePackReader reader, string field)
    {
        try
        {
            return reader.ReadBoolean();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Reading '{field}' as Boolean failed.", ex);

        }
    }

    internal static int ReadInt32(ref MessagePackReader reader, string field)
    {
        try
        {
            return reader.ReadInt32();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Reading '{field}' as Int32 failed.", ex);
        }
    }

    internal static string? ReadString(ref MessagePackReader reader, string field)
    {
        try
        {
            return reader.ReadString();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Reading '{field}' as String failed.", ex);
        }
    }

    internal static string ReadStringNotNull(ref MessagePackReader reader, string field)
    {
        string? result = null;
        try
        {
            result = reader.ReadString();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Reading '{field}' as String failed.", ex);

        }

        if (result == null)
        {
            throw new InvalidDataException($"Reading '{field}' as Not-Null String failed.");
        }

        return result;
    }

    internal static string? ReadString(ref MessagePackReader reader, string formatField, int param)
    {
        try
        {
            return reader.ReadString();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Reading '{string.Format(formatField, param)}' as String failed.", ex);
        }
    }

    internal static string[] ReadStringArray(ref MessagePackReader reader, string field)
    {
        var arrayLength = ReadArrayLength(ref reader, field);
        if (arrayLength > 0)
        {
            var array = new string[arrayLength];
            for (int i = 0; i < arrayLength; i++)
            {
                var fieldName = $"{field}[{i}]";
                array[i] = ReadStringNotNull(ref reader, fieldName);
            }

            return array;
        }

        return [];
    }

    internal static byte[] ReadBytes(ref MessagePackReader reader, string field)
    {
        try
        {
            return reader.ReadBytes()?.ToArray() ?? Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Reading '{field}' as Byte[] failed.", ex);
        }
    }

    internal static byte[] ReadBytes(ref MessagePackReader reader, string formatField, int param)
    {
        try
        {
            return reader.ReadBytes()?.ToArray() ?? Array.Empty<byte>();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Reading '{string.Format(formatField, param)}' as Byte[] failed.", ex);
        }
    }

    internal static ReadOnlySequence<byte>? ReadByteSequence(ref MessagePackReader reader, string field)
    {
        try
        {
            return reader.ReadBytes();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Reading binary sequence for '{field}' failed.", ex);
        }
    }

    internal static long ReadMapLength(ref MessagePackReader reader, string field)
    {
        try
        {
            return reader.ReadMapHeader();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Reading map length for '{field}' failed.", ex);
        }
    }

    internal static long ReadArrayLength(ref MessagePackReader reader, string field)
    {
        try
        {
            return reader.ReadArrayHeader();
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Reading array length for '{field}' failed.", ex);
        }
    }
}
