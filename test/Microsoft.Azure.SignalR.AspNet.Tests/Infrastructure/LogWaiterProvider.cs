// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    /// <summary>
    /// Use this provider to wait for some specific log to be logged
    /// </summary>
    internal class LogWaiterProvider : ILoggerProvider
    {
        private readonly ConcurrentDictionary<EventId, TaskCompletionSource<object>> _logs = new ConcurrentDictionary<EventId, TaskCompletionSource<object>>();

        public ILogger CreateLogger(string categoryName)
        {
            return new LogWaiterLogger(categoryName, this);
        }

        public Task WaitFor(EventId eventId)
        {
            var tcs = _logs.GetOrAdd(eventId, i => new TaskCompletionSource<object>());
            return tcs.Task;
        }

        public void Dispose()
        {
        }

        public void Log<TState>(string categoryName, LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var tcs = _logs.GetOrAdd(eventId, i => new TaskCompletionSource<object>());
            tcs.TrySetResult(null);
        }

        private class LogWaiterLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly LogWaiterProvider _provider;

            public LogWaiterLogger(string categoryName, LogWaiterProvider provider)
            {
                _categoryName = categoryName;
                _provider = provider;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                _provider.Log(_categoryName, logLevel, eventId, state, exception, formatter);
            }
        }
    }
}