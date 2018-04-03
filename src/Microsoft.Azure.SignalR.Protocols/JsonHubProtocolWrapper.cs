// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Internal.Formatters;
using Microsoft.AspNetCore.SignalR.Internal.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR
{
    public class JsonHubProtocolWrapper : IHubProtocol
    {
        private static readonly UTF8Encoding _utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private const string ErrorPropertyName          = "error";
        private const string TypePropertyName           = "type";
        private const string FormatPropertyName         = "format";
        private const string InvocationTypePropertyName = "invocationtype";
        private const string JsonPayloadPropertyName    = "jsonpayload";
        private const string MsgpackPayloadPropertyName = "msgpackpayload";
        private const string HeadersPropertyName        = "headers";

        public static string ProtocolName               = "jsonwrapper";

        public static readonly int ProtocolVersion = 1;

        public string Name => ProtocolName;

        public TransferFormat TransferFormat => TransferFormat.Text;

        public int Version => ProtocolVersion;

        public bool IsVersionSupported(int version)
        {
            return version == Version;
        }

        public bool TryParseMessages(ReadOnlyMemory<byte> input, IInvocationBinder binder, IList<HubMessage> messages)
        {
            while (TextMessageParser.TryParseMessage(ref input, out var payload))
            {
                var textReader = new Utf8BufferTextReader(payload);
                var message = ParseMessage(textReader);
                if (message != null)
                {
                    messages.Add(message);
                }
            }

            return messages.Count > 0;
        }

        private HubMessage ParseMessage(TextReader textReader)
        {
            int? type = null;
            int? protocolInt = null;
            int? targetType = null;
            string error = null;
            string jsonPayload = null;
            string msgpackPayload = null;
            Dictionary<string, string> headers = null;
            var completed = false;
            try
            {
                using (var reader = new JsonTextReader(textReader))
                {
                    reader.ArrayPool = JsonArrayPool<char>.Shared;

                    JsonUtils.CheckRead(reader);

                    // We're always parsing a JSON object
                    if (reader.TokenType != JsonToken.StartObject)
                    {
                        throw new InvalidDataException($"Unexpected JSON Token Type '{JsonUtils.GetTokenString(reader.TokenType)}'. Expected a JSON Object.");
                    }
                    do
                    {
                        switch (reader.TokenType)
                        {
                            case JsonToken.PropertyName:
                                string memberName = reader.Value.ToString();
                                switch (memberName)
                                {
                                    case TypePropertyName:
                                        var messageType = JsonUtils.ReadAsInt32(reader, TypePropertyName);

                                        if (messageType == null)
                                        {
                                            throw new InvalidDataException($"Missing required property '{TypePropertyName}'.");
                                        }

                                        type = messageType.Value;
                                        break;
                                    case FormatPropertyName:
                                        protocolInt = JsonUtils.ReadAsInt32(reader, FormatPropertyName);
                                        break;
                                    case InvocationTypePropertyName:
                                        targetType = JsonUtils.ReadAsInt32(reader, InvocationTypePropertyName);
                                        break;
                                    case HeadersPropertyName:
                                        JsonUtils.CheckRead(reader);
                                        headers = ReadHeaders(reader);
                                        break;
                                    case JsonPayloadPropertyName:
                                        jsonPayload = JsonUtils.ReadAsString(reader, JsonPayloadPropertyName);
                                        break;
                                    case MsgpackPayloadPropertyName:
                                        msgpackPayload = JsonUtils.ReadAsString(reader, MsgpackPayloadPropertyName);
                                        break;
                                    case ErrorPropertyName:
                                        error = JsonUtils.ReadAsString(reader, ErrorPropertyName);
                                        break;
                                    default:
                                        // Skip read the property name
                                        JsonUtils.CheckRead(reader);
                                        // Skip the value for this property
                                        reader.Skip();
                                        break;
                                }
                                break;
                            case JsonToken.EndObject:
                                completed = true;
                                break;
                        }
                    }
                    while (!completed && JsonUtils.CheckRead(reader));
                }

                switch (type)
                {
                    case AzureHubProtocolConstants.HubInvocationMessageWrapperType:
                        var hubMessageWrapper = new HubInvocationMessageWrapper((TransferFormat)protocolInt);
                        hubMessageWrapper.InvocationType = (HubInvocationType)(targetType.Value);
                        hubMessageWrapper.AddMetadata(headers);
                        if (jsonPayload != null)
                        {
                            hubMessageWrapper.JsonPayload = Convert.FromBase64String(jsonPayload);
                        }
                        if (msgpackPayload != null)
                        {
                            hubMessageWrapper.MsgpackPayload = Convert.FromBase64String(msgpackPayload);
                        }
                        return hubMessageWrapper;
                    case HubProtocolConstants.PingMessageType:
                        return PingMessage.Instance;
                    case HubProtocolConstants.CloseMessageType:
                        return BindCloseMessage(error);
                    default:
                        throw new InvalidDataException($"Unknown message type: {type}");
                }
            }
            catch (JsonReaderException jrex)
            {
                throw new InvalidDataException("Error reading JSON.", jrex);
            }
        }

        private static CloseMessage BindCloseMessage(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                return CloseMessage.Empty;
            }

            var message = new CloseMessage(error);
            return message;
        }

        private static Dictionary<string, string> ReadHeaders(JsonTextReader reader)
        {
            var headers = new Dictionary<string, string>();

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new InvalidDataException($"Expected '{HeadersPropertyName}' to be of type {JTokenType.Object}.");
            }

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonToken.PropertyName:
                        string propertyName = reader.Value.ToString();

                        JsonUtils.CheckRead(reader);

                        if (reader.TokenType != JsonToken.String)
                        {
                            throw new InvalidDataException($"Expected header '{propertyName}' to be of type {JTokenType.String}.");
                        }

                        headers[propertyName] = reader.Value?.ToString();
                        break;
                    case JsonToken.Comment:
                        break;
                    case JsonToken.EndObject:
                        return headers;
                }
            }

            throw new JsonReaderException("Unexpected end when reading message headers");
        }

        public void WriteMessage(HubMessage message, Stream output)
        {
            WriteMessageCore(message, output);
            TextMessageFormatter.WriteRecordSeparator(output);
        }

        private void WriteMessageCore(HubMessage message, Stream stream)
        {
            using (var writer = new JsonTextWriter(new StreamWriter(stream, _utf8NoBom, 1024, leaveOpen: true)))
            {
                writer.WriteStartObject();
                switch (message)
                {
                    case HubInvocationMessageWrapper hubInvocationMessageWrapper:
                        WriteInvocationMessageWrapper(hubInvocationMessageWrapper, writer);
                        break;
                    case PingMessage ping:
                        WriteMessageType(writer, HubProtocolConstants.PingMessageType);
                        break;
                    case CloseMessage m:
                        WriteMessageType(writer, HubProtocolConstants.CloseMessageType);
                        WriteCloseMessage(m, writer);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported message type: {message.GetType().FullName}");
                }
                writer.WriteEndObject();
            }
        }

        private static void WriteCloseMessage(CloseMessage message, JsonTextWriter writer)
        {
            if (!string.IsNullOrEmpty(message.Error))
            {
                writer.WritePropertyName(ErrorPropertyName);
                writer.WriteValue(message.Error);
            }
        }

        private static void WriteMessageType(JsonTextWriter writer, int type)
        {
            writer.WritePropertyName(TypePropertyName);
            writer.WriteValue(type);
        }

        private void WriteInvocationMessageWrapper(HubInvocationMessageWrapper message, JsonTextWriter writer)
        {   
            WriteMessageType(writer, AzureHubProtocolConstants.HubInvocationMessageWrapperType);
            WriteProtocolFormat(writer, message.Format);
            WriteInvocationType(writer, message.InvocationType);
            WriteHubInvocationMessageMeta(message, writer);
            if (message.JsonPayload != null)
            {
                WriteJsonPayload(writer, message.JsonPayload);
            }
            if (message.MsgpackPayload != null)
            {
                WriteMsgpackPayload(writer, message.MsgpackPayload);
            }
        }

        private static void WriteHubInvocationMessageMeta(HubInvocationMessageWrapper message, JsonTextWriter writer)
        {
            writer.WritePropertyName(HeadersPropertyName);
            writer.WriteStartObject();
            foreach (var kvp in message.Headers)
            {
                writer.WritePropertyName(kvp.Key);
                writer.WriteValue(kvp.Value);
            }
            writer.WriteEndObject();
        }

        private static void WriteProtocolFormat(JsonTextWriter writer, TransferFormat type)
        {
            writer.WritePropertyName(FormatPropertyName);
            writer.WriteValue(type);
        }

        private static void WriteInvocationType(JsonTextWriter writer, HubInvocationType type)
        {
            writer.WritePropertyName(InvocationTypePropertyName);
            writer.WriteValue(type);
        }

        private void WriteJsonPayload(JsonTextWriter writer, byte[] payload)
        {
            writer.WritePropertyName(JsonPayloadPropertyName);
            writer.WriteValue(Convert.ToBase64String(payload));
        }

        private void WriteMsgpackPayload(JsonTextWriter writer, byte[] payload)
        {
            writer.WritePropertyName(MsgpackPayloadPropertyName);
            writer.WriteValue(Convert.ToBase64String(payload));
        }
    }
}
