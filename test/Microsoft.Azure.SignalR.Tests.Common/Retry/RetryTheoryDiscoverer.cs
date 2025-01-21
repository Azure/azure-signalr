// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Testing.xunit;

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.Azure.SignalR.Tests;

public class RetryTheoryDiscoverer(IMessageSink diagnosticMessageSink) : TheoryDiscoverer(diagnosticMessageSink)
{
    protected override IEnumerable<IXunitTestCase> CreateTestCasesForDataRow(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo theoryAttribute,
        object[] dataRow)
    {
        var text = testMethod.EvaluateSkipConditions();
        if (text is not null)
        {
            return base.CreateTestCasesForSkippedDataRow(discoveryOptions, testMethod, theoryAttribute, dataRow, text);
        }
        var maxRetries = theoryAttribute.GetNamedArgument<int>(nameof(RetryTheoryAttribute.MaxRetries));
        var delayBetweenRetriesMs =
            theoryAttribute.GetNamedArgument<int>(nameof(RetryTheoryAttribute.DelayBetweenRetriesMs));
        var skipOnExceptions =
            theoryAttribute.GetNamedArgument<Type[]>(nameof(RetryTheoryAttribute.SkipOnExceptions));
        return
        [
            new RetryTestCase(
                DiagnosticMessageSink,
                discoveryOptions.MethodDisplayOrDefault(),
                testMethod,
                maxRetries,
                delayBetweenRetriesMs,
                skipOnExceptions,
                dataRow)
        ];
    }

    protected override IEnumerable<IXunitTestCase> CreateTestCasesForTheory(ITestFrameworkDiscoveryOptions discoveryOptions,
                                                                            ITestMethod testMethod,
                                                                            IAttributeInfo theoryAttribute)
    {
        var text = testMethod.EvaluateSkipConditions();
        if (text is not null)
        {
            return (IEnumerable<IXunitTestCase>)(object)new SkippedTestCase[1]
            {
                new(text, base.DiagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), testMethod)
            };
        }

        var maxRetries = theoryAttribute.GetNamedArgument<int>(nameof(RetryTheoryAttribute.MaxRetries));
        var delayBetweenRetriesMs =
            theoryAttribute.GetNamedArgument<int>(nameof(RetryTheoryAttribute.DelayBetweenRetriesMs));
        var skipOnExceptions =
            theoryAttribute.GetNamedArgument<Type[]>(nameof(RetryTheoryAttribute.SkipOnExceptions));

        return
        [
            new RetryTheoryDiscoveryAtRuntimeCase(DiagnosticMessageSink,
                                                  discoveryOptions.MethodDisplayOrDefault(),
                                                  testMethod,
                                                  maxRetries,
                                                  delayBetweenRetriesMs,
                                                  skipOnExceptions)
        ];
    }
}
