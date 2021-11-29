// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Microsoft.AspNetCore.Internal;

# nullable enable

/// <summary>
/// Copied from https://github.com/dotnet/aspnetcore/blob/d9660d157627af710b71c636fa8cb139616cadba/src/SignalR/common/Shared/ReusableUtf8JsonWriter.cs
/// </summary>
internal sealed class ReusableUtf8JsonWriter
{
    [ThreadStatic]
    private static ReusableUtf8JsonWriter? _cachedInstance;

    private readonly Utf8JsonWriter _writer;

#if DEBUG
    private bool _inUse;
#endif

    public ReusableUtf8JsonWriter(IBufferWriter<byte> stream)
    {
        _writer = new Utf8JsonWriter(stream, new JsonWriterOptions()
        {
#if !DEBUG
                SkipValidation = true,
#endif
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    public static ReusableUtf8JsonWriter Get(IBufferWriter<byte> stream)
    {
        var writer = _cachedInstance;
        if (writer == null)
        {
            writer = new ReusableUtf8JsonWriter(stream);
        }

        // Taken off the thread static
        _cachedInstance = null;
#if DEBUG
        if (writer._inUse)
        {
            throw new InvalidOperationException("The writer wasn't returned!");
        }

        writer._inUse = true;
#endif
        writer._writer.Reset(stream);
        return writer;
    }

    public static void Return(ReusableUtf8JsonWriter writer)
    {
        _cachedInstance = writer;

        writer._writer.Reset();

#if DEBUG
        writer._inUse = false;
#endif
    }

    public Utf8JsonWriter GetJsonWriter()
    {
        return _writer;
    }
}