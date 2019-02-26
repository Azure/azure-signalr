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
        private readonly ITraceManager _traceManager;

        public TraceManagerLoggerProvider(ITraceManager traceManager)
        {
            _traceManager = traceManager;
        }

        public ILogger CreateLogger(string categoryName)
        {
            var traceSource = _traceManager[categoryName];
            return new InternalTraceSourceLogger(traceSource);
        }

        public void Dispose()
        {
        }

        /// <summary>
        /// Use an InternalTraceSourceLogger to get rid of LogicalOperationStack inside the TraceSourceScope
        /// </summary>
        private class InternalTraceSourceLogger : ILogger
        {
            private readonly ILogger _inner;

            public InternalTraceSourceLogger(TraceSource traceSource)
            {
                _inner = new TraceSourceLogger(traceSource);
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                _inner.Log(logLevel, eventId, state, exception, formatter);
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return _inner.IsEnabled(logLevel);
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }
        }
    }
}