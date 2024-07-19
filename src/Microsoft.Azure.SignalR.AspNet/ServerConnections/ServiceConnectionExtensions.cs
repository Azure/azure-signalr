// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.Azure.SignalR.Protocol;
using Newtonsoft.Json;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal static class ServiceConnectionExtensions
    {
        public static Task WriteAsync(
            this ClientConnectionContext connection,
            string connectionId,
            object value,
            IServiceProtocol protocol,
            JsonSerializer serializer,
            IMemoryPool pool)
        {
            using var writer = new MemoryPoolTextWriter(pool);
            serializer.Serialize(writer, value);
            writer.Flush();

            // Reuse ConnectionDataMessage to wrap the payload
            var wrapped = new ConnectionDataMessage(string.Empty, writer.Buffer);
            var message = new ConnectionDataMessage(connectionId, protocol.GetMessageBytes(wrapped));
            return connection.WriteMessageAsync(message);
        }
    }
}