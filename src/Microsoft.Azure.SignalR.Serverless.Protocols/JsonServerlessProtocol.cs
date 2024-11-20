// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
#nullable enable

using System;
using System.Buffers;
using System.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.SignalR.Serverless.Protocols;

public class JsonServerlessProtocol : IServerlessProtocol
{
    private const string TypePropertyName = "type";

    public int Version => 1;

    public bool TryParseMessage(ref ReadOnlySequence<byte> input, out ServerlessMessage? message)
    {
        message = null;
        using var textReader = new JsonTextReader(new StreamReader(new ReadOnlySequenceStream(input)));
        var jObject = JObject.Load(textReader);
        if (jObject.TryGetValue(TypePropertyName, StringComparison.OrdinalIgnoreCase, out var token))
        {
            var type = token.Value<int>();
            message = type switch
            {
                ServerlessProtocolConstants.InvocationMessageType => SafeParseMessage<InvocationMessage>(jObject),
                ServerlessProtocolConstants.OpenConnectionMessageType => SafeParseMessage<OpenConnectionMessage>(jObject),
                ServerlessProtocolConstants.CloseConnectionMessageType => SafeParseMessage<CloseConnectionMessage>(jObject),
                _ => null,
            };
        }
        return message != null;
    }

    private ServerlessMessage? SafeParseMessage<T>(JObject jObject) where T : ServerlessMessage
    {
        try
        {
            return jObject.ToObject<T>();
        }
        catch
        {
            return null;
        }
    }
}