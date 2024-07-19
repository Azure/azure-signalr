using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Tests.Common;

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

    public static async Task RetryWhenExceptionThrows(Func<Task> asyncFunc, int maxCount = 3)
    {
        AssertActualExpectedException last = null;
        int i;
        for (i = 0; i < maxCount; i++)
        {
            try
            {
                await asyncFunc();
                break;
            }
            catch (AssertActualExpectedException e)
            {
                last = e;
                continue;
            }
        }
        if (i == maxCount && last != null)
        {
            throw last;
        }
    }

}
