// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.SignalR.Protocol;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    internal static class ReadOnlyMemoryExtension
    {
        private static readonly ServiceProtocol DefaultServiceProtocol = new ServiceProtocol();

        public static string GetSingleFramePayload(this ReadOnlyMemory<byte> payload)
        {
            var buffer = new ReadOnlySequence<byte>(payload);
            DefaultServiceProtocol.TryParseMessage(ref buffer, out var message);
            var frame = message as ConnectionDataMessage;
            Assert.NotNull(frame);
            var msg = Encoding.UTF8.GetString(frame.Payload.First.ToArray());
            Assert.NotNull(msg);
            var response = JsonConvert.DeserializeObject<Response>(msg);
            Assert.NotNull(response);
            Assert.Equal("0", response.C);
            Assert.Single(response.M);
            return response.M[0];
        }

        private sealed class Response
        {
            public string C { get; set; }
            public List<string> M { get; set; }
        }
    }
}