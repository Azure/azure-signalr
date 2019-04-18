// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Azure.SignalR
{
    internal class ConnectionFactory : IConnectionFactory
    {
        // Fix issue: https://github.com/Azure/azure-signalr/issues/198
        // .NET Framework has restriction about reserved string as the header name like "User-Agent"
        private static readonly Dictionary<string, string> CustomHeader = new Dictionary<string, string> { { "Asrs-User-Agent", ProductInfo.GetProductInfo() } };

        private readonly IServiceEndpointProvider _provider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly string _userId;
        private readonly string _hubName;

        public ConnectionFactory(string hubName, IServiceEndpointProvider provider, IServerNameProvider nameProvider, ILoggerFactory loggerFactory)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _loggerFactory = loggerFactory == null ? (ILoggerFactory)NullLoggerFactory.Instance : new GracefulLoggerFactory(loggerFactory);
            _userId = nameProvider?.GetName();
            _hubName = hubName;
        }

        public async Task<ConnectionContext> ConnectAsync(TransferFormat transferFormat, string connectionId, string target, CancellationToken cancellationToken = default)
        {
            var httpConnectionOptions = new HttpConnectionOptions
            {
                Url = GetServiceUrl(connectionId, target),
                AccessTokenProvider = () => Task.FromResult(_provider.GenerateServerAccessToken(_hubName, _userId)),
                Transports = HttpTransportType.WebSockets,
                SkipNegotiation = true,
                Headers = CustomHeader
            };
            var httpConnection = new HttpConnection(httpConnectionOptions, _loggerFactory);
            try
            {
                await httpConnection.StartAsync(transferFormat, cancellationToken);
                return httpConnection;
            }
            catch
            {
                await httpConnection.DisposeAsync();
                throw;
            }
        }

        private Uri GetServiceUrl(string connectionId, string target)
        {
            var baseUri = new UriBuilder(_provider.GetServerEndpoint(_hubName));
            var query = "cid=" + connectionId;
            if (target != null)
            {
                query = $"{query}&target={WebUtility.UrlEncode(target)}";
            }
            if (baseUri.Query != null && baseUri.Query.Length > 1)
            {
                baseUri.Query = baseUri.Query.Substring(1) + "&" + query;
            }
            else
            {
                baseUri.Query = query;
            }
            return baseUri.Uri;
        }

        public Task DisposeAsync(ConnectionContext connection)
        {
            if (connection == null)
            {
                return Task.CompletedTask;
            }

            return ((HttpConnection)connection).DisposeAsync();
        }

        private sealed class GracefulLoggerFactory : ILoggerFactory
        {
            private readonly ILoggerFactory _inner;
            public GracefulLoggerFactory(ILoggerFactory inner)
            {
                _inner = inner;
            }

            public void Dispose()
            {
                _inner.Dispose();
            }

            public ILogger CreateLogger(string categoryName)
            {
                var innerLogger = _inner.CreateLogger(categoryName);
                return new GracefulLogger(innerLogger);
            }

            public void AddProvider(ILoggerProvider provider)
            {
                _inner.AddProvider(provider);
            }

            private sealed class GracefulLogger : ILogger
            {
                private readonly ILogger _inner;
                public GracefulLogger(ILogger inner)
                {
                    _inner = inner;
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
                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
                {
                    if (logLevel >= LogLevel.Error)
                    {
                        logLevel = LogLevel.Warning;
                    }
                    _inner.Log(logLevel, eventId, state, null, formatter);
                }

                public bool IsEnabled(LogLevel logLevel)
                {
                    return _inner.IsEnabled(logLevel);
                }

                public IDisposable BeginScope<TState>(TState state)
                {
                    return _inner.BeginScope(state);
                }
            }
        }
    }
}
