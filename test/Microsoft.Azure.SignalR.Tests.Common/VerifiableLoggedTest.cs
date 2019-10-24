using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.Tests.Common
{
    public class VerifiableLoggedTest : LoggedTest
    {
        public VerifiableLoggedTest(ITestOutputHelper output) : base(output)
        {
        }

        public virtual IDisposable StartVerifiableLog(out ILoggerFactory loggerFactory, [CallerMemberName] string testName = null,Func<WriteContext, bool> expectedErrors = null)
        {
            var disposable = StartLog(out loggerFactory, testName);

            return new VerifyLogScope(loggerFactory, disposable, expectedErrors);
        }

        public virtual IDisposable StartVerifiableLog(out ILoggerFactory loggerFactory, LogLevel minLogLevel, [CallerMemberName] string testName = null,
            Func<WriteContext, bool> expectedErrors = null, Func<IList<LogRecord>, bool> logChecker = null)
        {
            var disposable = StartLog(out loggerFactory, minLogLevel, testName);

            return new VerifyLogScope(loggerFactory, disposable, expectedErrors, logChecker);
        }
    }
}
