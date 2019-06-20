// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace ChatSample
{
    public static class LoggerExtension
    {
        public static ILoggingBuilder AddTimedConsole(this ILoggingBuilder builder)
        {
            var services = builder.Services;
            builder.Services.TryAddSingleton<ConsoleLoggerProvider>();
            var defaultConsole = services.FirstOrDefault(s => s.ServiceType == typeof(ILoggerProvider) && s.ImplementationType == typeof(ConsoleLoggerProvider));
            if (defaultConsole != null)
            {
                services.Remove(defaultConsole);
            }

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, TimeConsoleLoggerProvider>());
            return builder;
        }

        public class TimeConsoleLoggerProvider : ILoggerProvider
        {
            private readonly ILoggerProvider _inner;

            public TimeConsoleLoggerProvider(ConsoleLoggerProvider inner)
            {
                _inner = inner;
            }

            public ILogger CreateLogger(string categoryName)
            {
                var logger = _inner.CreateLogger(categoryName);
                return new TimedLogger(logger);
            }

            public void Dispose()
            {
                _inner.Dispose();
            }
        }

        private class TimedLogger : ILogger
        {
            private readonly ILogger _logger;

            public TimedLogger(ILogger logger) => _logger = logger;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                _logger.Log(logLevel, eventId, state, exception, (s, ex) => $"[{DateTime.UtcNow:HH:mm:ss.fff}]: {formatter(s, ex)}");
            }

            public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);

            public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);
        }
    }
}
