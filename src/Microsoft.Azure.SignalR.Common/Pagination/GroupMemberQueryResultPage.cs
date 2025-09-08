// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using Azure;

#nullable enable

namespace Microsoft.Azure.SignalR;

[JsonConverter(typeof(GroupMemberQueryResultPageConverter))]
internal class GroupMemberQueryResultPage : Page<SignalRGroupConnection>
{
    public GroupMemberQueryResultPage(IReadOnlyList<SignalRGroupConnection> values, string? continuationToken)
    {
        Values = values ?? throw new ArgumentNullException(nameof(values));
        ContinuationToken = continuationToken;
    }

    public override IReadOnlyList<SignalRGroupConnection> Values { get; }

    public override string? ContinuationToken { get; }

    public override Response GetRawResponse()
    {
        // This class is for both WebSocket and REST API transports, therefore it does not have a raw response.
        // We can add it later for REST API if needed.
        throw new NotSupportedException();
    }
}

internal class GroupMemberQueryResultPageConverter : JsonConverter<GroupMemberQueryResultPage>
{
    public override GroupMemberQueryResultPage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        IReadOnlyList<SignalRGroupConnection>? values = null;
        string? continuationToken = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            string propertyName = reader.GetString()!;
            reader.Read();

            switch (propertyName)
            {
                case string s when s.Equals("value", StringComparison.OrdinalIgnoreCase):
                    values = JsonSerializer.Deserialize<List<SignalRGroupConnection>>(ref reader, options);
                    break;
                case string s when s.Equals("nextlink", StringComparison.OrdinalIgnoreCase):
                    continuationToken = JsonSerializer.Deserialize<string>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        return new GroupMemberQueryResultPage(values ?? new List<SignalRGroupConnection>(), continuationToken);
    }

    public override void Write(Utf8JsonWriter writer, GroupMemberQueryResultPage value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("value");
        JsonSerializer.Serialize(writer, value.Values, options);
        writer.WritePropertyName("nextLink");
        JsonSerializer.Serialize(writer, value.ContinuationToken, options);
        writer.WriteEndObject();
    }
}
