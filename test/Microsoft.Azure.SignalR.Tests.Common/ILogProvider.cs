using System;
using System.Collections.Generic;

namespace Microsoft.Azure.SignalR.Tests.Common;

public interface ILogProvider : IDisposable
{
    public IList<LogRecord> GetLogs();
}