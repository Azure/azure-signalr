// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
#if NET5_0_OR_GREATER
using System.Text.Json.Serialization;
#endif
using Azure.Core.Serialization;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Implements the SignalR Hub Protocol using <see cref="global::Azure.Core.Serialization.ObjectSerializer"/>.
    /// Modified from https://github.com/dotnet/aspnetcore/blob/d9660d157627af710b71c636fa8cb139616cadba/src/SignalR/common/Protocols.Json/src/Protocol/JsonHubProtocol.cs
    /// </summary>
    /// <remarks>
    /// Changes compared to original version:
    ///  <list>
    ///     <item> Change <see cref="TryParseMessage(ref ReadOnlySequence{byte}, IInvocationBinder, out HubMessage)"/> to unsupported as we don't need it. Related codes removed.</item>
    ///     <item> Use <see cref="global::Azure.Core.Serialization.ObjectSerializer"/> instead of <see cref="JsonSerializer"/> in the serialization. </item>
    /// </list>
    /// </remarks>
    internal sealed class JsonObjectSerializerHubProtocol : IHubProtocol
    {
        private const string ResultPropertyName = "result";
        private static readonly JsonEncodedText ResultPropertyNameBytes = JsonEncodedText.Encode(ResultPropertyName);
        private const string ItemPropertyName = "item";
        private static readonly JsonEncodedText ItemPropertyNameBytes = JsonEncodedText.Encode(ItemPropertyName);
        private const string InvocationIdPropertyName = "invocationId";
        private static readonly JsonEncodedText InvocationIdPropertyNameBytes = JsonEncodedText.Encode(InvocationIdPropertyName);
#if NETCOREAPP3_0_OR_GREATER
        private const string StreamIdsPropertyName = "streamIds";
        private static readonly JsonEncodedText StreamIdsPropertyNameBytes = JsonEncodedText.Encode(StreamIdsPropertyName);
#endif
        private const string TypePropertyName = "type";
        private static readonly JsonEncodedText TypePropertyNameBytes = JsonEncodedText.Encode(TypePropertyName);
        private const string ErrorPropertyName = "error";
        private static readonly JsonEncodedText ErrorPropertyNameBytes = JsonEncodedText.Encode(ErrorPropertyName);
        private const string TargetPropertyName = "target";
        private static readonly JsonEncodedText TargetPropertyNameBytes = JsonEncodedText.Encode(TargetPropertyName);
        private const string ArgumentsPropertyName = "arguments";
        private static readonly JsonEncodedText ArgumentsPropertyNameBytes = JsonEncodedText.Encode(ArgumentsPropertyName);
        private const string HeadersPropertyName = "headers";
        private static readonly JsonEncodedText HeadersPropertyNameBytes = JsonEncodedText.Encode(HeadersPropertyName);
        private static readonly byte[] CommaBytes = Encoding.UTF8.GetBytes(",");

        private const string ProtocolName = "json";
        private const int ProtocolVersion = 1;

        public ObjectSerializer ObjectSerializer { get; }

        public JsonObjectSerializerHubProtocol() : this(new JsonObjectSerializer())
        {
        }

        public JsonObjectSerializerHubProtocol(ObjectSerializer objectSerializer)
        {
            ObjectSerializer = objectSerializer;
        }

        /// <inheritdoc />
        public string Name => ProtocolName;

        /// <inheritdoc />
        public int Version => ProtocolVersion;

        /// <inheritdoc />
        public TransferFormat TransferFormat => TransferFormat.Text;

        /// <inheritdoc />
        public bool IsVersionSupported(int version)
        {
            return version == Version;
        }

        public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, out HubMessage message)
        {
            //We don't need reading message with this protocol.
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
        {
            WriteMessageCore(message, output);
            TextMessageFormatter.WriteRecordSeparator(output);
        }

        /// <inheritdoc />
        public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
        {
            return HubProtocolExtensions.GetMessageBytes(this, message);
        }

        private void WriteMessageCore(HubMessage message, IBufferWriter<byte> stream)
        {
            var reusableWriter = ReusableUtf8JsonWriter.Get(stream);

            try
            {
                var writer = reusableWriter.GetJsonWriter();
                writer.WriteStartObject();
                switch (message)
                {
                    case InvocationMessage m:
                        WriteMessageType(writer, HubProtocolConstants.InvocationMessageType);
                        WriteHeaders(writer, m);
                        // Partially use objectSerializer
                        WriteInvocationMessage(m, writer, stream);
                        break;
                    case StreamInvocationMessage m:
                        WriteMessageType(writer, HubProtocolConstants.StreamInvocationMessageType);
                        WriteHeaders(writer, m);
                        // Partially use objectSerializer
                        WriteStreamInvocationMessage(m, writer, stream);
                        break;
                    case StreamItemMessage m:
                        WriteMessageType(writer, HubProtocolConstants.StreamItemMessageType);
                        WriteHeaders(writer, m);
                        // Partially use objectSerializer
                        WriteStreamItemMessage(m, writer, stream);
                        break;
                    case CompletionMessage m:
                        WriteMessageType(writer, HubProtocolConstants.CompletionMessageType);
                        WriteHeaders(writer, m);
                        // Partially use objectSerializer
                        WriteCompletionMessage(m, writer, stream);
                        break;
                    case CancelInvocationMessage m:
                        WriteMessageType(writer, HubProtocolConstants.CancelInvocationMessageType);
                        WriteHeaders(writer, m);
                        WriteCancelInvocationMessage(m, writer);
                        break;
                    case PingMessage _:
                        WriteMessageType(writer, HubProtocolConstants.PingMessageType);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported message type: {message.GetType().FullName}");
                }
                writer.WriteEndObject();
                writer.Flush();
                Debug.Assert(writer.CurrentDepth == 0);
            }
            finally
            {
                ReusableUtf8JsonWriter.Return(reusableWriter);
            }
        }

        private static void WriteHeaders(Utf8JsonWriter writer, HubInvocationMessage message)
        {
            if (message.Headers != null && message.Headers.Count > 0)
            {
                writer.WriteStartObject(HeadersPropertyNameBytes);
                foreach (var value in message.Headers)
                {
                    writer.WriteString(value.Key, value.Value);
                }
                writer.WriteEndObject();
            }
        }

        private void WriteCompletionMessage(CompletionMessage message, Utf8JsonWriter writer, IBufferWriter<byte> bufferWriter)
        {
            WriteInvocationId(message, writer);
            if (!string.IsNullOrEmpty(message.Error))
            {
                writer.WriteString(ErrorPropertyNameBytes, message.Error);
            }
            else if (message.HasResult)
            {
                writer.WritePropertyName(ResultPropertyNameBytes);
                if (message.Result == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    WriteWithObjectSerializer(message.Result, writer, bufferWriter);
                }
            }
        }

        private static void WriteCancelInvocationMessage(CancelInvocationMessage message, Utf8JsonWriter writer)
        {
            WriteInvocationId(message, writer);
        }

        private void WriteStreamItemMessage(StreamItemMessage message, Utf8JsonWriter writer, IBufferWriter<byte> bufferWriter)
        {
            WriteInvocationId(message, writer);

            writer.WritePropertyName(ItemPropertyNameBytes);
            if (message.Item == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                WriteWithObjectSerializer(message.Item, writer, bufferWriter);
            }
        }

        private void WriteInvocationMessage(InvocationMessage message, Utf8JsonWriter writer, IBufferWriter<byte> bufferWriter)
        {
            WriteInvocationId(message, writer);
            writer.WriteString(TargetPropertyNameBytes, message.Target);

            WriteArguments(message.Arguments, writer, bufferWriter);

#if NETCOREAPP3_0_OR_GREATER
            WriteStreamIds(message.StreamIds, writer);
#endif
        }

        private void WriteStreamInvocationMessage(StreamInvocationMessage message, Utf8JsonWriter writer, IBufferWriter<byte> bufferWriter)
        {
            WriteInvocationId(message, writer);
            writer.WriteString(TargetPropertyNameBytes, message.Target);

            WriteArguments(message.Arguments, writer, bufferWriter);

#if NETCOREAPP3_0_OR_GREATER
            WriteStreamIds(message.StreamIds, writer);
#endif
        }

        private void WriteArguments(object?[]? arguments, Utf8JsonWriter writer, IBufferWriter<byte> bufferWriter)
        {
            if (arguments == null)
            {
                return;
            }
            writer.WriteStartArray(ArgumentsPropertyNameBytes);
            for (var i = 0; i < arguments.Length; i++)
            {
                var argument = arguments[i];
                if (argument == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    WriteWithObjectSerializer(argument, writer, bufferWriter);
                    if (i != arguments.Length - 1)
                    {
                        bufferWriter.Write(CommaBytes);
                    }
                }
            }
            writer.WriteEndArray();
        }

        private void WriteWithObjectSerializer(object obj, Utf8JsonWriter utf8JsonWriter, IBufferWriter<byte> bufferWriter)
        {
            utf8JsonWriter.Flush();
            var binaryData = ObjectSerializer.Serialize(obj);
            bufferWriter.Write(binaryData.ToMemory().Span);
        }

#if NETCOREAPP3_0_OR_GREATER
        private static void WriteStreamIds(string[]? streamIds, Utf8JsonWriter writer)
        {
            if (streamIds == null)
            {
                return;
            }

            writer.WriteStartArray(StreamIdsPropertyNameBytes);
            foreach (var streamId in streamIds)
            {
                writer.WriteStringValue(streamId);
            }
            writer.WriteEndArray();
        }
#endif

        private static void WriteInvocationId(HubInvocationMessage message, Utf8JsonWriter writer)
        {
            if (!string.IsNullOrEmpty(message.InvocationId))
            {
                writer.WriteString(InvocationIdPropertyNameBytes, message.InvocationId);
            }
        }

        private static void WriteMessageType(Utf8JsonWriter writer, int type)
        {
            writer.WriteNumber(TypePropertyNameBytes, type);
        }

        internal static JsonSerializerOptions CreateDefaultSerializerSettings()
        {
            return new JsonSerializerOptions()
            {
                WriteIndented = false,
                ReadCommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
#if NET5_0_OR_GREATER
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
#endif
                IgnoreReadOnlyProperties = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                MaxDepth = 64,
                DictionaryKeyPolicy = null,
                DefaultBufferSize = 16 * 1024,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
        }
    }
}
