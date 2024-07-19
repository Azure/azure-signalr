// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Json;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.Azure.SignalR.Protocol;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests;

internal static class SignalRMessageUtility
{
    private static readonly ServiceProtocol DefaultServiceProtocol = new ServiceProtocol();
    private static readonly JsonSerializer DefaultJsonSerializer = new JsonSerializer();
    private static readonly MemoryPool DefaultPool = new MemoryPool();

    public static ReadOnlyMemory<byte> GenerateSingleFrameBuffer(this ReadOnlyMemory<byte> inner)
    {
        var singleFrameMessage = new ConnectionDataMessage(string.Empty, inner);
        return DefaultServiceProtocol.GetMessageBytes(singleFrameMessage);
    }

    public static ReadOnlyMemory<byte> GenerateSingleFrameBuffer(this string message)
    {
        var inner = Encoding.UTF8.GetBytes(message);
        var singleFrameMessage = new ConnectionDataMessage(string.Empty, inner);
        return DefaultServiceProtocol.GetMessageBytes(singleFrameMessage);
    }

    public static T GetJsonMessageFromSingleFramePayload<T>(this ReadOnlyMemory<byte> payload) where T : class
    {
        var buffer = new ReadOnlySequence<byte>(payload);
        DefaultServiceProtocol.TryParseMessage(ref buffer, out var message);
        var frame = message as ConnectionDataMessage;
        Assert.NotNull(frame);
        var msg = Encoding.UTF8.GetString(frame.Payload.First.ToArray());
        Assert.NotNull(msg);
        var response = JsonConvert.DeserializeObject<Response<T>>(msg);
        Assert.NotNull(response);
        Assert.Equal("0", response.C);
        Assert.Single(response.M);
        return response.M[0];
    }

    public static Message CreateMessage(string key, object value)
    {
        ArraySegment<byte> messageBuffer = GetMessageBuffer(value);

        var message = new Message(Guid.NewGuid().ToString("N"), key, messageBuffer);

        var command = value as Command;
        if (command != null)
        {
            // Set the command id
            message.CommandId = command.Id;
            message.WaitForAck = command.WaitForAck;
        }

        return message;
    }

    private static ArraySegment<byte> GetMessageBuffer(object value)
    {
        ArraySegment<byte> messageBuffer;
        // We can't use "as" like we do for Command since ArraySegment is a struct
        if (value is ArraySegment<byte>)
        {
            // We assume that any ArraySegment<byte> is already JSON serialized
            messageBuffer = (ArraySegment<byte>)value;
        }
        else
        {
            messageBuffer = SerializeMessageValue(value);
        }
        return messageBuffer;
    }

    private static ArraySegment<byte> SerializeMessageValue(object value)
    {
        using (var writer = new MemoryPoolTextWriter(DefaultPool))
        {

            var selfSerializer = value as IJsonWritable;

            if (selfSerializer != null)
            {
                selfSerializer.WriteJson(writer);
            }
            else
            {
                DefaultJsonSerializer.Serialize(writer, value);
            }

            writer.Flush();

            var data = writer.Buffer;

            var buffer = new byte[data.Count];

            Buffer.BlockCopy(data.Array, data.Offset, buffer, 0, data.Count);

            return new ArraySegment<byte>(buffer);
        }
    }

    private sealed class Response<T>
    {
        public string C { get; set; }
        public List<T> M { get; set; }
    }
}