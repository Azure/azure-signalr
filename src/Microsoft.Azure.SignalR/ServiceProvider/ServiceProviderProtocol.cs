// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Buffers;

namespace Microsoft.Azure.SignalR
{
    internal class ServiceProviderProtocol
    {
        private const string ServiceUrlPropertyName = "url";
        private const string AccessTokenPropertyName = "accessToken";

        public static void WriteResponse(ServiceProviderResponse response, IBufferWriter<byte> output)
        {
            var textWriter = Utf8BufferTextWriter.Get(output);
            try
            {
                using (var jsonWriter = JsonUtils.CreateJsonTextWriter(textWriter))
                {
                    jsonWriter.WriteStartObject();
                    jsonWriter.WritePropertyName(ServiceUrlPropertyName);
                    jsonWriter.WriteValue(response.ServiceUrl);
                    jsonWriter.WritePropertyName(AccessTokenPropertyName);
                    jsonWriter.WriteValue(response.AccessToken);
                    jsonWriter.WriteEndObject();

                    jsonWriter.Flush();
                }
            }
            finally
            {
                Utf8BufferTextWriter.Return(textWriter);
            }
        }
    }
}
