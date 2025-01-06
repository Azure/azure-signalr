// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using MessagePack;

namespace Microsoft.Azure.SignalR.Protocol;

#nullable enable

public class ServiceModelProtocol
{
    public bool TryParseModel<T>(ReadOnlySequence<byte> input, out T? model) where T : notnull, new()
    {
        if (typeof(IMessagePackSerializable).IsAssignableFrom(typeof(T)))
        {
            model = new T();
            var reader = new MessagePackReader(input);
            ((IMessagePackSerializable)model).Load(ref reader, typeof(T).Name);
            return true;
        }
        else
        {
            model = default;
            return false;
        }
    }

    public T ParseModel<T>(ReadOnlySequence<byte> input) where T : notnull, new()
    {
        if (typeof(IMessagePackSerializable).IsAssignableFrom(typeof(T)))
        {
            var model = new T();
            var reader = new MessagePackReader(input);
            ((IMessagePackSerializable)model).Load(ref reader, typeof(T).Name);
            return model;
        }
        else
        {
            throw new NotSupportedException($"Type {typeof(T).Name} is not supported.");
        }
    }

    public void WriteModel<T>(T model, IBufferWriter<byte> output) where T : notnull, new()
    {
        if (typeof(IMessagePackSerializable).IsAssignableFrom(typeof(T)))
        {
            var writer = new MessagePackWriter(output);
            ((IMessagePackSerializable)model).Serialize(ref writer);
            writer.Flush();
        }
        else
        {
            throw new NotSupportedException($"Type {typeof(T).Name} is not supported.");
        }
    }
}
