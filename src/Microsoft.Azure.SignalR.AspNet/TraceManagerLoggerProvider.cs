// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            return new TraceSourceLogger(traceSource);
        }

        public void Dispose()
        {
        }
    }
}