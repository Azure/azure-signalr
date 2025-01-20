// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Tests;

#nullable enable

public static class HandshakeUtils
{
    private static readonly TimeSpan DefaultHandshakeTimeout = TimeSpan.FromSeconds(5);

    private static readonly ServiceProtocol ServiceProtocol = new ServiceProtocol();

    public static async Task<HandshakeRequestMessage> ReceiveHandshakeRequestAsync(PipeReader input)
    {
        using var cts = new CancellationTokenSource(DefaultHandshakeTimeout);

        while (true)
        {
            var result = await input.ReadAsync(cts.Token);

            var buffer = result.Buffer;
            var consumed = buffer.Start;
            var examined = buffer.End;

            try
            {
                if (!buffer.IsEmpty)
                {
                    if (ServiceProtocol.TryParseMessage(ref buffer, out var message))
                    {
                        consumed = buffer.Start;
                        examined = consumed;

                        if (message is not HandshakeRequestMessage handshakeRequest)
                        {
                            throw new InvalidDataException(
                                $"{message.GetType().Name} received when waiting for handshake request.");
                        }

                        if (handshakeRequest.Version != ServiceProtocol.Version)
                        {
                            throw new InvalidDataException("Protocol version not supported.");
                        }

                        return handshakeRequest;
                    }
                }

                if (result.IsCompleted)
                {
                    // Not enough data, and we won't be getting any more data.
                    throw new InvalidOperationException(
                        "Service connectioned disconnected before sending a handshake request");
                }
            }
            finally
            {
                input.AdvanceTo(consumed, examined);
            }
        }
    }

    public static Task SendHandshakeResponseAsync(PipeWriter output, HandshakeResponseMessage response)
    {
        ServiceProtocol.WriteMessage(response, output);
        return output.FlushAsync().AsTask();
    }
}
