// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Tests.Common;

public class VerifiableLoggedTest(ITestOutputHelper output) : LoggedTest(output)
{
    public static async Task RetryWhenExceptionThrows(Func<Task> asyncFunc, int maxCount = 3)
    {
        NotEqualException last = null;
        int i;
        for (i = 0; i < maxCount; i++)
        {
            try
            {
                await asyncFunc();
                break;
            }
            catch (NotEqualException e)
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

    public virtual IVerifiableLog StartVerifiableLog(out ILoggerFactory loggerFactory, [CallerMemberName] string testName = null)
    {
        var disposable = StartLog(out loggerFactory, testName);

        return new VerifyLogScope(loggerFactory, disposable);
    }

    public virtual IVerifiableLog StartVerifiableLog(out ILoggerFactory loggerFactory,
                                                  LogLevel minLogLevel,
                                                  [CallerMemberName] string testName = null)
    {
        var disposable = StartLog(out loggerFactory, minLogLevel, testName);

        return new VerifyLogScope(loggerFactory, disposable);
    }
}
