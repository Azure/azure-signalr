// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR.E2ETests;

// copied from https://github.com/aspnet/AspNetCore/blob/release/3.0-preview7/src/SignalR/server/Core/src/Internal/DefaultHubProtocolResolver.cs
internal sealed class DefaultHubProtocolResolver : IHubProtocolResolver
{
    private readonly ILogger<DefaultHubProtocolResolver> _logger;

    private readonly List<IHubProtocol> _hubProtocols;

    private readonly Dictionary<string, IHubProtocol> _availableProtocols;

    public IReadOnlyList<IHubProtocol> AllProtocols => _hubProtocols;

    public DefaultHubProtocolResolver(IEnumerable<IHubProtocol> availableProtocols, ILogger<DefaultHubProtocolResolver> logger)
    {
        _logger = logger ?? NullLogger<DefaultHubProtocolResolver>.Instance;
        _availableProtocols = new Dictionary<string, IHubProtocol>(StringComparer.OrdinalIgnoreCase);

        // We might get duplicates in _hubProtocols, but we're going to check it and overwrite in just a sec.
        _hubProtocols = availableProtocols.ToList();
        foreach (var protocol in _hubProtocols)
        {
            Log.RegisteredSignalRProtocol(_logger, protocol.Name, protocol.GetType());
            _availableProtocols[protocol.Name] = protocol;
        }
    }

    public IHubProtocol GetProtocol(string protocolName, IReadOnlyList<string>? supportedProtocols)
    {
        protocolName = protocolName ?? throw new ArgumentNullException(nameof(protocolName));

        if (_availableProtocols.TryGetValue(protocolName, out var protocol) && (supportedProtocols == null || supportedProtocols.Contains(protocolName, StringComparer.OrdinalIgnoreCase)))
        {
            Log.FoundImplementationForProtocol(_logger, protocolName);
            return protocol;
        }
        throw new NotSupportedException(protocolName);
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Type, Exception?> Registered =
            LoggerMessage.Define<string, Type>(LogLevel.Debug, new EventId(1, "RegisteredSignalRProtocol"), "Registered SignalR Protocol: {ProtocolName}, implemented by {ImplementationType}.");

        private static readonly Action<ILogger, string, Exception?> Found =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, "FoundImplementationForProtocol"), "Found protocol implementation for requested protocol: {ProtocolName}.");

        public static void RegisteredSignalRProtocol(ILogger logger, string protocolName, Type implementationType)
        {
            Registered(logger, protocolName, implementationType, null);
        }

        public static void FoundImplementationForProtocol(ILogger logger, string protocolName)
        {
            Found(logger, protocolName, null);
        }
    }
}