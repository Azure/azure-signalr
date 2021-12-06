// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#nullable enable

namespace Microsoft.Azure.SignalR.Management
{
    /// <summary>
    /// Modifed based on https://github.com/dotnet/aspnetcore/blob/d9660d157627af710b71c636fa8cb139616cadba/src/SignalR/server/Core/src/Internal/DefaultHubProtocolResolver.cs
    /// Compared to the origin protocol resolver, it adds logic to decides whether to replace <see cref="JsonHubProtocol"/> with <see cref="JsonObjectSerializerHubProtocol"/>.
    /// </summary>
    internal class JsonHubProtocolResolver : IHubProtocolResolver
    {
        private readonly ILogger<JsonHubProtocolResolver> _logger;
        private readonly List<IHubProtocol> _hubProtocols;
        private readonly Dictionary<string, IHubProtocol> _availableProtocols;

        public IReadOnlyList<IHubProtocol> AllProtocols => _hubProtocols;

        public JsonHubProtocolResolver(IEnumerable<IHubProtocol> availableProtocols, ILogger<JsonHubProtocolResolver> logger)
        {
            _logger = logger ?? NullLogger<JsonHubProtocolResolver>.Instance;
            _availableProtocols = new Dictionary<string, IHubProtocol>(StringComparer.OrdinalIgnoreCase);

            foreach (var protocol in availableProtocols)
            {
                // decide whether replace the default JSON protocol.
                if (protocol is JsonObjectSerializerHubProtocol serializerHubProtocol && serializerHubProtocol.Usable is not true)
                {
                    Log.SkipJsonObjectSerializerProtocol(_logger);
                    continue;
                }
                _availableProtocols[protocol.Name] = protocol;
                Log.RegisteredSignalRProtocol(_logger, protocol.Name, protocol.GetType());
            }
            _hubProtocols = _availableProtocols.Values.ToList();
        }

        public virtual IHubProtocol? GetProtocol(string protocolName, IReadOnlyList<string>? supportedProtocols)
        {
            protocolName = protocolName ?? throw new ArgumentNullException(nameof(protocolName));

            if (_availableProtocols.TryGetValue(protocolName, out var protocol) && (supportedProtocols == null || supportedProtocols.Contains(protocolName, StringComparer.OrdinalIgnoreCase)))
            {
                Log.FoundImplementationForProtocol(_logger, protocolName);
                return protocol;
            }

            // null result indicates protocol is not supported
            // result will be validated by the caller
            return null;
        }

        private static class Log
        {
            // Category: JsonHubProtocolResolver
            private static readonly Action<ILogger, string, Type, Exception?> _registeredSignalRProtocol =
                LoggerMessage.Define<string, Type>(LogLevel.Debug, new EventId(1, "RegisteredSignalRProtocol"), "Registered SignalR Protocol: {ProtocolName}, implemented by {ImplementationType}.");

            private static readonly Action<ILogger, string, Exception?> _foundImplementationForProtocol =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "FoundImplementationForProtocol"), "Found protocol implementation for requested protocol: {ProtocolName}.");

            private static readonly Action<ILogger, Exception?> _skipJsonObjectSerializerProtocol =
                LoggerMessage.Define(LogLevel.Debug, new EventId(3, "SkipJsonObjectSerializerProtocol"), $"Skip {nameof(JsonObjectSerializerHubProtocol)} because it is not usable.");


            public static void RegisteredSignalRProtocol(ILogger logger, string protocolName, Type implementationType)
            {
                _registeredSignalRProtocol(logger, protocolName, implementationType, null);
            }

            public static void FoundImplementationForProtocol(ILogger logger, string protocolName)
            {
                _foundImplementationForProtocol(logger, protocolName, null);
            }

            public static void SkipJsonObjectSerializerProtocol(ILogger logger)
            {
                _skipJsonObjectSerializerProtocol(logger, null);
            }
        }
    }
}
