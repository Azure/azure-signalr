using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    internal class VerifyLogScope : IDisposable
    {
        private readonly IDisposable _wrappedDisposable;
        private readonly Func<WriteContext, bool> _expectedErrors;
        private readonly Func<IList<LogRecord>, bool> _logChecker;
        private readonly LogSinkProvider _sink;

        public ILoggerFactory LoggerFactory { get; }

        public VerifyLogScope(ILoggerFactory loggerFactory = null, IDisposable wrappedDisposable = null, Func<WriteContext, bool> expectedErrors = null, Func<IList<LogRecord>, bool> logChecker = null)
        {
            _wrappedDisposable = wrappedDisposable;
            _expectedErrors = expectedErrors;
            _logChecker = logChecker;
            _sink = new LogSinkProvider();

            LoggerFactory = loggerFactory ?? new LoggerFactory();
            LoggerFactory.AddProvider(_sink);
        }

        public void Dispose()
        {
            _wrappedDisposable?.Dispose();

            var logs = _sink.GetLogs();
            if (_logChecker?.Invoke(logs) == false)
            {
                throw new Exception("Failed checking log");
            }

            var results = _sink.GetLogs().Where(w => w.Write.LogLevel >= LogLevel.Error).ToList();

            if (_expectedErrors != null)
            {
                if (!results.Any(w => _expectedErrors(w.Write)))
                {
                    throw new Exception("Fail to match expected error(s).");
                }
                results = results.Where(w => !_expectedErrors(w.Write)).ToList();
            }

            if (results.Count > 0)
            {
                string errorMessage = $"{results.Count} error(s) logged.";
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

                throw new Exception(errorMessage);
            }
        }
    }
}
