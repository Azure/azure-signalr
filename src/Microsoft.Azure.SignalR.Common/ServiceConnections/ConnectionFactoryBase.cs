// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR;

#nullable enable

internal abstract class ConnectionFactoryBase : IConnectionFactory
{
    private readonly ILoggerFactory _loggerFactory;

    private readonly string _serverId;

    public ConnectionFactoryBase(IServerNameProvider nameProvider,
                                 ILoggerFactory loggerFactory)
    {
        _loggerFactory = new GracefulLoggerFactory(loggerFactory);
        _serverId = nameProvider.GetName();
    }

    public async Task<ConnectionContext> ConnectAsync(HubServiceEndpoint hubServiceEndpoint,
                                                      TransferFormat transferFormat,
                                                      string connectionId,
                                                      string target,
                                                      CancellationToken cancellationToken = default)
    {
        var provider = hubServiceEndpoint.Provider;
        var hubName = hubServiceEndpoint.Hub;

        var accessTokenProvider = provider.GetServerAccessTokenProvider(hubName, _serverId);
        var url = GetServiceUrl(provider, hubName, connectionId, target);

        var connectionOptions = new WebSocketConnectionOptions
        {
            Headers = GetRequestHeaders(),
            Proxy = provider.Proxy,
        };
        var connection = new WebSocketConnectionContext(connectionOptions, _loggerFactory, accessTokenProvider);

        try
        {
            await connection.StartAsync(url, cancellationToken);

            return connection;
        }
        catch
        {
            await connection.StopAsync();
            throw;
        }
    }

    public Task DisposeAsync(ConnectionContext connection)
    {
        if (connection == null)
        {
            return Task.CompletedTask;
        }
        return ((WebSocketConnectionContext)connection).StopAsync();
    }

    internal IDictionary<string, string> GetRequestHeaders()
    {
        var headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        SetCustomHeaders(headers);
        CheckHeaders(headers, Constants.Headers.AsrsHeaderPrefix);
        CheckHeaders(headers, Constants.Headers.AsrsInternalHeaderPrefix);
        SetInternalHeaders(headers);
        return headers;
    }

    internal virtual void SetInternalHeaders(IDictionary<string, string> headers)
    {
        // Fix issue: https://github.com/Azure/azure-signalr/issues/198
        // .NET Framework has restriction about reserved string as the header name like "User-Agent"
        headers[Constants.AsrsUserAgent] = ProductInfo.GetProductInfo();

        if (!string.IsNullOrEmpty(_serverId) && !headers.ContainsKey(Constants.Headers.AsrsServerId))
        {
            headers.Add(Constants.Headers.AsrsServerId, _serverId);
        }
    }

    protected abstract void SetCustomHeaders(IDictionary<string, string> headers);

    private static void CheckHeaders(IDictionary<string, string> headers, string forbidPrefix)
    {
        var item = headers.Where(x => x.Key.StartsWith(forbidPrefix, StringComparison.InvariantCultureIgnoreCase));
        if (item.Any())
        {
            var key = item.First().Key;
            throw new ArgumentException($"Invalid header {key}, custom header cannot startwith '{forbidPrefix}'");
        }
    }
    private static Uri GetServiceUrl(IServiceEndpointProvider provider, string hubName, string connectionId, string target)
    {
        var baseUri = new UriBuilder(provider.GetServerEndpoint(hubName));
        var query = "cid=" + connectionId;
        if (target != null)
        {
            query = $"{query}&target={WebUtility.UrlEncode(target)}";
        }
        if (baseUri.Query != null && baseUri.Query.Length > 1)
        {
#if NET6_0_OR_GREATER
            baseUri.Query = string.Concat(baseUri.Query.AsSpan(1), "&", query);
#else

            baseUri.Query = baseUri.Query.Substring(1) + "&" + query;
#endif
        }
        else
        {
            baseUri.Query = query;
        }
        return baseUri.Uri;
    }

    private sealed class GracefulLoggerFactory : ILoggerFactory
    {
        private readonly ILoggerFactory _inner;

        public GracefulLoggerFactory(ILoggerFactory inner)
        {
            _inner = inner;
        }

        public void AddProvider(ILoggerProvider provider)
        {
            _inner.AddProvider(provider);
        }

        public ILogger CreateLogger(string categoryName)
        {
            var innerLogger = _inner.CreateLogger(categoryName);
            return new GracefulLogger(innerLogger);
        }

        public void Dispose()
        {
            _inner.Dispose();
        }
        private sealed class GracefulLogger : ILogger
        {
            private readonly ILogger _inner;

            public GracefulLogger(ILogger inner)
            {
                _inner = inner;
            }

#nullable disable
            public IDisposable BeginScope<TState>(TState state)
            {
                return _inner.BeginScope(state);
            }
#nullable enable

            public bool IsEnabled(LogLevel logLevel)
            {
                return _inner.IsEnabled(logLevel);
            }

            /// <summary>
            /// Downgrade error level logs, and also exclude exception details
            /// Exceptions thrown from inside the HttpConnection are supposed to be handled by the caller and logged with more user-friendly message
            /// </summary>
            /// <typeparam name="TState"></typeparam>
            /// <param name="logLevel"></param>
            /// <param name="eventId"></param>
            /// <param name="state"></param>
            /// <param name="exception"></param>
            /// <param name="formatter"></param>
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Error)
                {
                    logLevel = LogLevel.Warning;
                }
                _inner.Log(logLevel, eventId, state, null, formatter);
            }
        }
    }
}
