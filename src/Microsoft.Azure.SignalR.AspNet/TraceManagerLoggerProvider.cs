// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.TraceSource;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class TraceManagerLoggerProvider : ILoggerProvider
    {
        private readonly TraceSource _traceSource;

        public TraceManagerLoggerProvider(ITraceManager traceManager)
        {
            // For all the Azure SignalR traces, share the same trace source for least config requirements
            _traceSource = traceManager["Microsoft.Azure.SignalR"];
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new InternalTraceSourceLogger(_traceSource, categoryName);
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Use an InternalTraceSourceLogger to get rid of LogicalOperationStack inside the TraceSourceScope
        /// Slightly different from the TraceSourceLogger in that the message is prefixed with [categoryName]
        /// </summary>
        private class InternalTraceSourceLogger : ILogger
        {
            private readonly TraceSource _traceSource;
            private readonly string _categoryName;

            public InternalTraceSourceLogger(TraceSource traceSource, string categoryName)
            {
                _traceSource = traceSource;
                _categoryName = string.IsNullOrEmpty(categoryName) ? string.Empty : $"[{categoryName}]";
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }
                var message = string.Empty;
                if (formatter != null)
                {
                    message = formatter(state, exception);
                }
                else
                {
                    if (state != null)
                    {
                        message += state;
                    }
                    if (exception != null)
                    {
                        message += Environment.NewLine + exception;
                    }
                }

                if (!string.IsNullOrEmpty(message))
                {
                    // use 0 to keep consistency with ASP.NET SignalR trace pattern
                    _traceSource.TraceEvent(GetEventType(logLevel), 0, _categoryName + message);
                }
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                if (logLevel == LogLevel.None)
                {
                    return false;
                }

                var traceEventType = GetEventType(logLevel);
                return _traceSource.Switch.ShouldTrace(traceEventType);
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }

            private static TraceEventType GetEventType(LogLevel logLevel)
            {
                switch (logLevel)
                {
                    case LogLevel.Critical: return TraceEventType.Critical;
                    case LogLevel.Error: return TraceEventType.Error;
                    case LogLevel.Warning: return TraceEventType.Warning;
                    case LogLevel.Information: return TraceEventType.Information;
                    case LogLevel.Trace:
                    default: return TraceEventType.Verbose;
                }
            }
        }
    }
}