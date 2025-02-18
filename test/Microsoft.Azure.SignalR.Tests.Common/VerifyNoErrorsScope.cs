// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

using Xunit;

namespace Microsoft.Azure.SignalR.Tests.Common;

internal class VerifyLogScope : IVerifiableLog
{
    private readonly IDisposable _wrappedDisposable;
    private readonly LogSinkProvider _sink;

    private readonly List<Func<LogRecord, bool>> _expectedLogs = new();

    public ILoggerFactory LoggerFactory { get; }

    public VerifyLogScope(ILoggerFactory loggerFactory = null, IDisposable wrappedDisposable = null)
    {
        _wrappedDisposable = wrappedDisposable;
        _sink = new LogSinkProvider();

        LoggerFactory = loggerFactory ?? new LoggerFactory();
        LoggerFactory.AddProvider(_sink);
    }

    public LogRecord Expects(string logEventName) => Expects(i => i.Write.EventId.Name == logEventName);

    public LogRecord Expects(Func<LogRecord, bool> predicate)
    {
        var matches = ExpectsMany(predicate);
        Assert.NotEmpty(matches);
        return matches[0];
    }

    public IReadOnlyList<LogRecord> ExpectsMany(string logEventName) => ExpectsMany(i => i.Write.EventId.Name == logEventName);

    public IReadOnlyList<LogRecord> ExpectsMany(Func<LogRecord, bool> predicate)
    {
        _expectedLogs.Add(predicate);
        return _sink.GetLogs().Where(predicate).ToArray();
    }

    public void Dispose()
    {
        _wrappedDisposable?.Dispose();
        var results = _sink.GetLogs().Where(i =>
        {
            // Only check unexpected error logs
            if (i.Write.LogLevel < LogLevel.Error)
            {
                return false;
            }

            foreach (var filter in _expectedLogs)
            {
                if (filter(i))
                {
                    return false;
                }
            }

            return true;
        }).ToArray();
        if (results.Length > 0)
        {
            string errorMessage = $"{results.Length} error(s) logged.";
            errorMessage += Environment.NewLine;
            errorMessage += string.Join(Environment.NewLine, results.Select(record =>
            {
                var r = record.Write;

                string lineMessage = r.LoggerName + " - " + r.EventId.ToString() + " - " + r.Formatter(r.State, r.Exception);
                if (r.Exception != null)
                {
                    lineMessage += Environment.NewLine;
                    lineMessage += "===================";
                    lineMessage += Environment.NewLine;
                    lineMessage += r.Exception;
                    lineMessage += Environment.NewLine;
                    lineMessage += "===================";
                }
                return lineMessage;
            }));

            throw new InvalidOperationException(errorMessage);
        }
    }
}
