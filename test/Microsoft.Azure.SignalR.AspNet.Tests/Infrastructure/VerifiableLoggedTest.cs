using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit.Abstractions;

namespace Microsoft.Azure.SignalR.AspNet.Tests
{
    public class VerifiableLoggedTest : LoggedTest
    {
        public VerifiableLoggedTest(ITestOutputHelper output) : base(output)
        {
        }

        public virtual IDisposable StartVerifiableLog(out ILoggerFactory loggerFactory, [CallerMemberName] string testName = null, Func<WriteContext, bool> expectedErrorsFilter = null)
        {
            var disposable = StartLog(out loggerFactory, testName);

            return new VerifyNoErrorsScope(loggerFactory, disposable, expectedErrorsFilter);
        }

        public virtual IDisposable StartVerifiableLog(out ILoggerFactory loggerFactory, LogLevel minLogLevel, [CallerMemberName] string testName = null, Func<WriteContext, bool> expectedErrorsFilter = null)
        {
            var disposable = StartLog(out loggerFactory, minLogLevel, testName);

            return new VerifyNoErrorsScope(loggerFactory, disposable, expectedErrorsFilter);
        }
    }
}
