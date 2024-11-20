// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using MessagePack;

namespace Microsoft.Azure.SignalR.Serverless.Protocols;

internal static class MessagePackReaderExtensions
{
    public static int ReadInt32(ref this MessagePackReader reader, string field)
    {
        try
        {
            return reader.ReadInt32();
        }
        catch (Exception e)
        {
            throw new InvalidDataException($"Reading '{field}' as Int32 failed.", e);
        }
    }

    public static string ReadString(ref this MessagePackReader reader, string field)
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

    public static int ReadMapLength(ref this MessagePackReader reader, string field)
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

    public static int ReadArrayLength(ref this MessagePackReader reader, string field)
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

    public static object?[] ReadArray(ref this MessagePackReader reader, string field)
    {
        var count = reader.ReadArrayLength(field);
        var array = new object?[count];
        for (var i = 0; i < count; i++)
        {
            array[i] = reader.ReadObject($"the element at indext {i} of {field}");
        }
        return array;
    }

    public static object? ReadObject(ref this MessagePackReader reader, string field)
    {
        switch (reader.NextMessagePackType)
        {
            case MessagePackType.Binary: return reader.ReadBytes().GetValueOrDefault().ToArray();

            case MessagePackType.Integer: return reader.ReadInt64();
            case MessagePackType.Boolean: return reader.ReadBoolean();
            case MessagePackType.Float: return reader.ReadDouble();
            case MessagePackType.String: return reader.ReadString();
            case MessagePackType.Array: return reader.ReadArray(field);
            case MessagePackType.Map:
                var propertyCount = reader.ReadMapHeader();
                var map = new Dictionary<string, object?>();
                for (var i = 0; i < propertyCount; i++)
                {
                    var key = reader.ReadString();
                    var value = reader.ReadObject(field);
                    map[key] = value;
                }
                return map;
            case MessagePackType.Nil:
                reader.ReadNil();
                return null;
            case MessagePackType.Extension:
            case MessagePackType.Unknown:
            default:
                return null;
        }
    }

    public static void SkipHeader(ref this MessagePackReader reader)
    {
        var headerCount = reader.ReadMapLength("headers");
        if (headerCount > 0)
        {
            for (var i = 0; i < headerCount; i++)
            {
                reader.ReadString($"headers[{i}].Key");
                reader.ReadString($"headers[{i}].Value");
            }
        }
    }

}
